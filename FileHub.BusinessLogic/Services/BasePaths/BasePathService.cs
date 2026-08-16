using Dal.Repositories.BasePaths;
using Dtos.BasePaths;
using Entities.Paths;
using FileHub.BusinessLogic.Validation;
using Microsoft.Extensions.Logging;
using Shared;

namespace FileHub.BusinessLogic.Services.BasePaths;

public sealed class BasePathService : IBasePathService
{
    private readonly ILogger<BasePathService> m_logger;
    private readonly IBasePathRepository m_basePathRepository;

    public BasePathService(
        ILogger<BasePathService> logger,
        IBasePathRepository basePathRepository
    )
    {
        m_logger = logger;
        m_basePathRepository = basePathRepository;
    }

    public async Task<OperationResult<List<BasePathDto>>> GetAllAsync()
    {
        var basePaths = await m_basePathRepository.GetAllAsync();
        return OperationResult<List<BasePathDto>>.Success(basePaths.Select(MapBasePath).ToList());
    }

    public async Task<OperationResult<BasePathDto>> CreateAsync(SaveBasePathDto dto)
    {
        var validation = DtoValidator.Validate(dto);
        if (validation.HasError)
        {
            return validation.MapError<BasePathDto>();
        }

        var pathResult = NormalizeDirectory(dto.Path);
        if (pathResult.HasError)
        {
            return pathResult.MapError<BasePathDto>();
        }

        var path = pathResult.Value;
        if (await m_basePathRepository.PathExistsAsync(path, excludeId: null))
        {
            return OperationResult<BasePathDto>.BadRequest($"\"{path}\" is already a base path");
        }

        var basePath = new BasePath { Path = path, Name = ResolveName(dto.Name, path) };
        m_basePathRepository.Add(basePath);
        await m_basePathRepository.SaveChangesAsync();

        // Nobody can see it yet: a base path is invisible until it is granted, so creating one is
        // not by itself a change in what anyone — including the admin who created it — can read.
        m_logger.LogInformation("Created base path {BasePathId} at {Path}", basePath.Id, basePath.Path);
        return OperationResult<BasePathDto>.Success(MapBasePath(basePath));
    }

    public async Task<OperationResult<BasePathDto>> UpdateAsync(Guid id, SaveBasePathDto dto)
    {
        var validation = DtoValidator.Validate(dto);
        if (validation.HasError)
        {
            return validation.MapError<BasePathDto>();
        }

        var basePath = await m_basePathRepository.GetAsync(id);
        if (basePath is null)
        {
            return OperationResult<BasePathDto>.NotFound("Base path not found");
        }

        var pathResult = NormalizeDirectory(dto.Path);
        if (pathResult.HasError)
        {
            return pathResult.MapError<BasePathDto>();
        }

        var path = pathResult.Value;
        if (await m_basePathRepository.PathExistsAsync(path, excludeId: id))
        {
            return OperationResult<BasePathDto>.BadRequest($"\"{path}\" is already a base path");
        }

        // Repointing a base path silently repoints every link into it, because a share stores
        // (base path, relative path) and is re-resolved on every hit. That is the intended
        // behaviour — the alternative, a link that keeps serving the old directory, is worse.
        basePath.Path = path;
        basePath.Name = ResolveName(dto.Name, path);
        await m_basePathRepository.SaveChangesAsync();

        m_logger.LogInformation("Updated base path {BasePathId} to {Path}", basePath.Id, basePath.Path);
        return OperationResult<BasePathDto>.Success(MapBasePath(basePath));
    }

    public async Task<OperationResult<Empty>> DeleteAsync(Guid id)
    {
        var basePath = await m_basePathRepository.GetAsync(id);
        if (basePath is null)
        {
            return OperationResult<Empty>.NotFound("Base path not found");
        }

        // Grants and shares cascade with the row. The Go build had to delete shares by hand here,
        // because a share stored a resolved absolute path and outlived the base path it came from.
        m_basePathRepository.Remove(basePath);
        await m_basePathRepository.SaveChangesAsync();

        m_logger.LogInformation("Deleted base path {BasePathId} at {Path}", id, basePath.Path);
        return OperationResult<Empty>.Success();
    }

    public async Task<OperationResult<List<Guid>>> GetUsersAsync(Guid basePathId)
    {
        var basePath = await m_basePathRepository.GetAsync(basePathId);
        if (basePath is null)
        {
            return OperationResult<List<Guid>>.NotFound("Base path not found");
        }

        return OperationResult<List<Guid>>.Success(await m_basePathRepository.GetUserIdsAsync(basePathId));
    }

    public async Task<OperationResult<Empty>> SetUsersAsync(Guid basePathId, SetBasePathAccessDto dto)
    {
        var validation = DtoValidator.Validate(dto);
        if (validation.HasError)
        {
            return validation.MapError<Empty>();
        }

        var basePath = await m_basePathRepository.GetAsync(basePathId);
        if (basePath is null)
        {
            return OperationResult<Empty>.NotFound("Base path not found");
        }

        var userIds = dto.UserIds ?? [];
        await m_basePathRepository.ReplaceAccessForBasePathAsync(basePathId, userIds);
        await m_basePathRepository.SaveChangesAsync();

        m_logger.LogInformation(
            "Base path {BasePathId} is now granted to {UserCount} user(s)", basePathId, userIds.Count);
        return OperationResult<Empty>.Success();
    }

    public async Task<OperationResult<List<Guid>>> GetUserBasePathsAsync(Guid userId)
    {
        return OperationResult<List<Guid>>.Success(await m_basePathRepository.GetBasePathIdsAsync(userId));
    }

    public async Task<OperationResult<Empty>> SetUserBasePathsAsync(Guid userId, SetUserBasePathsDto dto)
    {
        var validation = DtoValidator.Validate(dto);
        if (validation.HasError)
        {
            return validation.MapError<Empty>();
        }

        var requested = dto.BasePathIds ?? [];

        // An id that matches no base path would fail on the foreign key at save time; drop it here
        // so a stale id in the admin UI does not take the whole grant list down with it.
        var known = (await m_basePathRepository.GetAllAsync()).Select(p => p.Id).ToHashSet();
        var basePathIds = requested.Where(known.Contains).ToList();

        await m_basePathRepository.ReplaceAccessForUserAsync(userId, basePathIds);
        await m_basePathRepository.SaveChangesAsync();

        m_logger.LogInformation("User {UserId} is now granted {Count} base path(s)", userId, basePathIds.Count);
        return OperationResult<Empty>.Success();
    }

    /// <summary>
    /// A base path is a directory on the host that has to be there now: an absolute path that does
    /// not exist is a typo, and storing it would show the user an empty listing rather than an error.
    /// Stored cleaned, without a trailing separator, so the sandbox's prefix check has one form to
    /// compare against.
    /// </summary>
    private static OperationResult<string> NormalizeDirectory(string path)
    {
        var trimmed = (path ?? string.Empty).Trim();

        if (!Path.IsPathRooted(trimmed))
        {
            return OperationResult<string>.BadRequest("The base path must be an absolute path");
        }

        string full;

        try
        {
            full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmed));
        }
        catch (ArgumentException)
        {
            return OperationResult<string>.BadRequest("The base path is not a valid path");
        }
        catch (PathTooLongException)
        {
            return OperationResult<string>.BadRequest("The base path is too long");
        }

        if (!Directory.Exists(full))
        {
            return OperationResult<string>.BadRequest($"\"{full}\" does not exist or is not a directory");
        }

        return OperationResult<string>.Success(full);
    }

    private static string ResolveName(string name, string path)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length > 0)
        {
            return trimmed;
        }

        // Falling back to the directory name, and to the path itself for a mount point like "/data"
        // whose GetFileName is empty.
        var directoryName = Path.GetFileName(path);
        return directoryName.Length > 0 ? directoryName : path;
    }

    private static BasePathDto MapBasePath(BasePath basePath) => new()
    {
        Id = basePath.Id,
        Path = basePath.Path,
        Name = basePath.Name,
        CreatedAt = basePath.CreatedAt,
        UserCount = basePath.Access.Count
    };
}
