using Dal.Repositories.BasePaths;
using Dtos.Files;
using Entities.Paths;
using FileHub.BusinessLogic.Authorization;
using FileHub.BusinessLogic.Validation;
using Microsoft.Extensions.Logging;
using Shared;

namespace FileHub.BusinessLogic.Services.Files;

public sealed class FileService : IFileService
{
    private readonly ILogger<FileService> m_logger;
    private readonly IBasePathRepository m_basePathRepository;

    public FileService(
        ILogger<FileService> logger,
        IBasePathRepository basePathRepository
    )
    {
        m_logger = logger;
        m_basePathRepository = basePathRepository;
    }

    public async Task<OperationResult<List<FileEntryDto>>> GetBasePathsAsync(Guid userId)
    {
        var basePaths = await m_basePathRepository.GetForUserAsync(userId);
        var entries = new List<FileEntryDto>(basePaths.Count);

        foreach (var basePath in basePaths)
        {
            if (!Directory.Exists(basePath.Path))
            {
                // A base path whose mount is gone is skipped rather than fatal — one unplugged disk
                // must not take the whole browser down.
                m_logger.LogWarning(
                    "Base path {BasePathId} points at {Path}, which is not a readable directory",
                    basePath.Id, basePath.Path);
                continue;
            }

            entries.Add(new FileEntryDto
            {
                Id = basePath.Id,
                Name = basePath.Name,
                IsDir = true,
                Size = CountEntries(basePath.Path),
                NextSegment = string.Empty,
                IsBasePath = true,
                ItemId = Guid.NewGuid()
            });
        }

        return OperationResult<List<FileEntryDto>>.Success(entries);
    }

    public async Task<OperationResult<NavigationDto>> NavigateAsync(Guid userId, NavigateDto dto)
    {
        var validation = DtoValidator.Validate(dto);
        if (validation.HasError)
        {
            return validation.MapError<NavigationDto>();
        }

        var basePath = await m_basePathRepository.GetForUserAsync(dto.BasePathId, userId);
        if (basePath is null)
        {
            return NotGranted<NavigationDto>(userId, dto.BasePathId);
        }

        var relativePath = dto.Path ?? string.Empty;

        if (!PathSandbox.TryResolve(basePath.Path, relativePath, out var fullPath))
        {
            m_logger.LogWarning(
                "User {UserId} tried to navigate outside base path {BasePathId} with \"{RelativePath}\"",
                userId, basePath.Id, relativePath);
            return OperationResult<NavigationDto>.NotFound(PathNotFound);
        }

        if (!Directory.Exists(fullPath))
        {
            return OperationResult<NavigationDto>.NotFound(PathNotFound);
        }

        var navigation = new NavigationDto
        {
            NavigationName = relativePath.Length == 0 ? basePath.Name : Path.GetFileName(fullPath),
            Entries = ListDirectory(basePath, fullPath)
        };

        return OperationResult<NavigationDto>.Success(navigation);
    }

    public async Task<OperationResult<ResolvedFile>> ResolveDownloadAsync(
        Guid userId, Guid basePathId, string relativePath)
    {
        var basePath = await m_basePathRepository.GetForUserAsync(basePathId, userId);
        if (basePath is null)
        {
            return NotGranted<ResolvedFile>(userId, basePathId);
        }

        var relative = relativePath ?? string.Empty;

        if (!PathSandbox.TryResolve(basePath.Path, relative, out var fullPath))
        {
            m_logger.LogWarning(
                "User {UserId} tried to download outside base path {BasePathId} with \"{RelativePath}\"",
                userId, basePathId, relative);
            return OperationResult<ResolvedFile>.NotFound(PathNotFound);
        }

        var isDirectory = Directory.Exists(fullPath);
        if (!isDirectory && !File.Exists(fullPath))
        {
            return OperationResult<ResolvedFile>.NotFound(PathNotFound);
        }

        var resolved = new ResolvedFile
        {
            FullPath = fullPath,
            Name = relative.Length == 0 ? basePath.Name : Path.GetFileName(fullPath),
            IsDirectory = isDirectory
        };

        m_logger.LogInformation("User {UserId} downloads {Path}", userId, fullPath);
        return OperationResult<ResolvedFile>.Success(resolved);
    }

    private List<FileEntryDto> ListDirectory(BasePath basePath, string directoryPath)
    {
        var entries = new List<FileEntryDto>();

        foreach (var entryPath in EnumerateSafely(directoryPath))
        {
            // Every listed entry is run back through the sandbox, so the listing shows only what the
            // caller could actually open: a symlink pointing off the base path is refused here
            // exactly as it would be on the download route, instead of appearing and then 404ing.
            var nextSegment = PathSandbox.ToRelative(basePath.Path, entryPath);

            if (!PathSandbox.TryResolve(basePath.Path, nextSegment, out _))
            {
                continue;
            }

            var entry = MapEntry(basePath.Id, entryPath, nextSegment);

            if (entry is null)
            {
                continue;
            }

            entries.Add(entry);
        }

        return entries
            .OrderByDescending(e => e.IsDir)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Null when the entry cannot be read — a race with a delete, or a permission the app
    /// does not have. One unreadable entry is skipped, it does not fail the listing.</summary>
    private FileEntryDto? MapEntry(Guid basePathId, string fullPath, string nextSegment)
    {
        try
        {
            var isDirectory = Directory.Exists(fullPath);

            return new FileEntryDto
            {
                Id = basePathId,
                Name = Path.GetFileName(fullPath),
                IsDir = isDirectory,
                Size = isDirectory ? CountEntries(fullPath) : new FileInfo(fullPath).Length,
                NextSegment = nextSegment,
                IsBasePath = false,
                ItemId = Guid.NewGuid()
            };
        }
        catch (IOException exception)
        {
            m_logger.LogDebug(exception, "Skipping unreadable entry {Path}", fullPath);
            return null;
        }
        catch (UnauthorizedAccessException exception)
        {
            m_logger.LogDebug(exception, "Skipping unreadable entry {Path}", fullPath);
            return null;
        }
    }

    private IEnumerable<string> EnumerateSafely(string directoryPath)
    {
        try
        {
            // Materialized here so a mid-enumeration IO error is caught by this try rather than
            // escaping from inside the caller's foreach.
            return Directory.EnumerateFileSystemEntries(directoryPath).ToList();
        }
        catch (IOException exception)
        {
            m_logger.LogWarning(exception, "Failed to list {Path}", directoryPath);
            return [];
        }
        catch (UnauthorizedAccessException exception)
        {
            m_logger.LogWarning(exception, "Failed to list {Path}", directoryPath);
            return [];
        }
    }

    /// <summary>
    /// A directory's "size" is the number of entries it holds, which is what the browser shows next
    /// to a folder. (The Go build reported the directory inode's own byte size minus two, which was
    /// the link count in disguise and wrong on most filesystems.)
    /// </summary>
    private static long CountEntries(string directoryPath)
    {
        try
        {
            return Directory.EnumerateFileSystemEntries(directoryPath).LongCount();
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    /// <summary>
    /// A base path the caller holds no grant for answers exactly like one that does not exist, so the
    /// response cannot be used to enumerate what other users can see.
    /// </summary>
    private OperationResult<T> NotGranted<T>(Guid userId, Guid basePathId)
    {
        m_logger.LogWarning("User {UserId} has no access to base path {BasePathId}", userId, basePathId);
        return OperationResult<T>.NotFound("Base path not found");
    }

    private const string PathNotFound = "Path not found";
}
