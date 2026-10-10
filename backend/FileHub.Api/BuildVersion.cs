using System.Text.Json;
using Microsoft.Extensions.FileProviders;

namespace FileHub;

/// <summary>
/// What release this image is, read from the <c>wwwroot/version.json</c> the Dockerfile writes.
/// A local run has no such file, and a plain <c>docker build</c> leaves the fields empty.
/// </summary>
public sealed record BuildVersion(string Version, string CommitSha, string BuiltAt)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public bool IsRelease => !string.IsNullOrWhiteSpace(Version);

    public static BuildVersion? Read(IFileProvider webRoot)
    {
        var file = webRoot.GetFileInfo("version.json");
        if (!file.Exists)
        {
            return null;
        }

        using var stream = file.CreateReadStream();
        return JsonSerializer.Deserialize<BuildVersion>(stream, JsonOptions);
    }

    // Startup must not fail over a version line, so an unreadable file reads as a development build.
    public static void LogAtStartup(WebApplication app)
    {
        BuildVersion? version;
        try
        {
            version = Read(app.Environment.WebRootFileProvider);
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            app.Logger.LogWarning(e, "wwwroot/version.json could not be read");
            version = null;
        }

        if (version is not { IsRelease: true })
        {
            app.Logger.LogInformation("Starting FileHub, development build");
            return;
        }

        app.Logger.LogInformation(
            "Starting FileHub {Version:l} (commit {CommitSha:l}, built {BuiltAt:l})",
            version.Version, version.CommitSha, version.BuiltAt);
    }
}
