using System.IO.Compression;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;
using Microsoft.Net.Http.Headers;

namespace FileHub.Downloads;

/// <summary>
/// The one way bytes leave this app, shared by the authenticated download route and the public
/// share route. Both hand it a path the sandbox has already validated — it does no authorization of
/// its own.
/// </summary>
public static class FileDownload
{
    /// <summary>Streams a file, or the directory as a zip built on the fly.</summary>
    public static IResult Create(HttpContext context, string fullPath, string name, bool isDirectory, ILogger logger)
    {
        AllowSlowClients(context);

        if (!isDirectory)
        {
            // fileDownloadName goes through ContentDispositionHeaderValue.SetHttpFileName inside
            // Results.File, which emits both a quoted ASCII filename and an RFC 2231 filename*.
            // Range processing is on so a paused or resumed download does not start over.
            return Results.File(fullPath, "application/octet-stream", name, enableRangeProcessing: true);
        }

        var zipName = string.IsNullOrWhiteSpace(name) ? "download.zip" : name + ".zip";

        // Set by hand rather than through Results.Stream's fileDownloadName so the encoding is
        // visible here: a quote or a non-ASCII character in a directory name truncates the name the
        // browser saves if the header is assembled by string concatenation.
        var contentDisposition = new ContentDispositionHeaderValue("attachment");
        contentDisposition.SetHttpFileName(zipName);
        context.Response.Headers.ContentDisposition = contentDisposition.ToString();

        // The archive is written through the async ZipArchive API below, but not every entry
        // operation has an async overload (a directory entry carries no stream to open). Kestrel
        // rejects a synchronous write to the response body by default, and that rejection would
        // land mid-stream, so the escape hatch is opened up front for this request only.
        AllowSynchronousWrites(context);

        return Results.Stream(stream => WriteZipAsync(stream, fullPath, logger), "application/zip");
    }

    /// <summary>
    /// Turns off Kestrel's minimum response data rate for this request. It is the .NET counterpart of
    /// the Go server deliberately having no <c>WriteTimeout</c>: a multi-gigabyte file or a zip that
    /// is still being built streams for as long as it takes, and the default 240 bytes/s floor would
    /// abort a slow client — or a slow disk — in the middle of a file.
    /// </summary>
    private static void AllowSlowClients(HttpContext context)
    {
        var dataRate = context.Features.Get<IHttpMinResponseDataRateFeature>();

        if (dataRate is not null)
        {
            dataRate.MinDataRate = null;
        }
    }

    private static void AllowSynchronousWrites(HttpContext context)
    {
        var bodyControl = context.Features.Get<IHttpBodyControlFeature>();

        if (bodyControl is not null)
        {
            bodyControl.AllowSynchronousIO = true;
        }
    }

    private static async Task WriteZipAsync(Stream output, string directoryPath, ILogger logger)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            // One unreadable subdirectory should cost that subdirectory, not the whole archive.
            IgnoreInaccessible = true,
            // Symlinks are not followed. The sandbox refuses one that leaves the base path, and this
            // walk has no sandbox of its own — following them here would be the escape hatch.
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        try
        {
            // The async factory and DisposeAsync matter here: the local headers and the central
            // directory are written through the response body, and Kestrel refuses synchronous
            // writes to it.
            await using var archive = await ZipArchive.CreateAsync(
                output, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: null);

            foreach (var entryPath in Directory.EnumerateFileSystemEntries(directoryPath, "*", options))
            {
                await AddEntryAsync(archive, directoryPath, entryPath);
            }
        }
        catch (IOException exception)
        {
            LogAborted(logger, exception, directoryPath);
        }
        catch (UnauthorizedAccessException exception)
        {
            LogAborted(logger, exception, directoryPath);
        }
    }

    private static async Task AddEntryAsync(ZipArchive archive, string root, string entryPath)
    {
        // Zip entry names are always forward-slashed, whatever the host separator is.
        var name = Path.GetRelativePath(root, entryPath).Replace(Path.DirectorySeparatorChar, '/');

        if (Directory.Exists(entryPath))
        {
            // A directory is its name plus a trailing slash and no content.
            archive.CreateEntry(name + "/");
            return;
        }

        // NoCompression, matching the Go build's zip.Store: the payload is usually already-compressed
        // media, so deflating it costs CPU on the download path and saves nothing.
        var entry = archive.CreateEntry(name, CompressionLevel.NoCompression);

        await using var source = File.OpenRead(entryPath);
        await using var target = await entry.OpenAsync();
        await source.CopyToAsync(target);
    }

    /// <summary>
    /// The status line and every byte so far are already on the wire, so there is no way left to tell
    /// the client anything: the archive simply ends unfinished and their unzip complains. Log it;
    /// do not try to write an error body.
    /// </summary>
    private static void LogAborted(ILogger logger, Exception exception, string directoryPath)
    {
        logger.LogWarning(exception, "Aborted building the zip for {Path}", directoryPath);
    }
}
