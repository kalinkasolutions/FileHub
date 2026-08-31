using Dal.Repositories.BasePaths;
using Dal.Repositories.Groups;
using Dal.Repositories.Shares;
using Dtos.BasePaths;
using Entities.Paths;
using FileHub.BusinessLogic.Auditing;
using FileHub.BusinessLogic.Validation;
using Microsoft.Extensions.Logging;
using Shared;

namespace FileHub.BusinessLogic.Services.BasePaths;

public sealed class BasePathService : IBasePathService
{
    private readonly ILogger<BasePathService> m_logger;
    private readonly IAuditActor m_auditActor;
    private readonly IBasePathRepository m_basePathRepository;
    private readonly IGroupRepository m_groupRepository;
    private readonly IShareRepository m_shareRepository;

    public BasePathService(
        ILogger<BasePathService> logger,
        IAuditActor auditActor,
        IBasePathRepository basePathRepository,
        IGroupRepository groupRepository,
        IShareRepository shareRepository
    )
    {
        m_logger = logger;
        m_auditActor = auditActor;
        m_basePathRepository = basePathRepository;
        m_groupRepository = groupRepository;
        m_shareRepository = shareRepository;
    }

    public async Task<OperationResult<List<BasePathDto>>> GetAllAsync()
    {
        var basePaths = await m_basePathRepository.GetAllAsync();
        return OperationResult<List<BasePathDto>>.Success(
            basePaths.Select(p => MapBasePath(p.BasePath, p.UserCount, p.GroupCount)).ToList());
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
        m_logger.LogInformation(
            "{Actor:l} created base path \"{Name:l}\" ({BasePathId}) at {Path:l}",
            m_auditActor.Describe(), basePath.Name, basePath.Id, basePath.Path);
        return OperationResult<BasePathDto>.Success(MapBasePath(basePath, userCount: 0, groupCount: 0));
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
        var previousPath = basePath.Path;
        basePath.Path = path;
        basePath.Name = ResolveName(dto.Name, path);
        await m_basePathRepository.SaveChangesAsync();

        m_logger.LogInformation(
            "{Actor:l} repointed base path \"{Name:l}\" ({BasePathId}) from {OldPath:l} to {Path:l}",
            m_auditActor.Describe(), basePath.Name, basePath.Id, previousPath, basePath.Path);

        // The counts are read rather than taken off the entity: nothing loaded the grant
        // collections here, so they were both 0 and the admin list — which replaces its row with
        // this answer — lost the grant chip on every rename until the next reload.
        var userIds = await m_basePathRepository.GetUserIdsAsync(id);
        var groupIds = await m_basePathRepository.GetGroupIdsAsync(id);
        return OperationResult<BasePathDto>.Success(MapBasePath(basePath, userIds.Count, groupIds.Count));
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

        m_logger.LogInformation(
            "{Actor:l} deleted base path \"{Name:l}\" ({BasePathId}) at {Path:l}; its grants and links went with it",
            m_auditActor.Describe(), basePath.Name, id, basePath.Path);
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

        // An id that matches no account would fail on the foreign key at save time; drop it here
        // so a stale id in the admin UI does not take the whole grant list down with it. Same
        // reasoning, and the same behaviour, as SetUserBasePathsAsync below.
        var userIds = await m_basePathRepository.FilterExistingUserIdsAsync(dto.UserIds ?? []);

        // Revoking a grant has to revoke the links made under it. Deleting the base path or the
        // user cascades and takes the links along; withdrawing a grant is the one direction the
        // foreign keys do not cover, and the anonymous download path deliberately carries no user
        // lookup to catch it later — so the creator loses navigation while their public link keeps
        // serving the file to anyone holding the URL.
        //
        // Only the direct grants are changing here, so the group grants are read as they stand and
        // passed along: a user who keeps the base path through a group keeps their links.
        //
        // Done before the grant change is saved, so a failure here leaves the links revoked and
        // the grant intact rather than the other way round.
        var groupIds = await m_basePathRepository.GetGroupIdsAsync(basePathId);
        var revoked = await m_shareRepository.DeleteSharesLosingBasePathAccessAsync(basePathId, userIds, groupIds);

        await m_basePathRepository.ReplaceAccessForBasePathAsync(basePathId, userIds);
        await m_basePathRepository.SaveChangesAsync();

        m_logger.LogInformation(
            "{Actor:l} granted base path \"{Name:l}\" ({BasePathId}) to {UserCount} user(s); "
            + "{ShareCount} link(s) revoked with it",
            m_auditActor.Describe(), basePath.Name, basePathId, userIds.Count, revoked);
        return OperationResult<Empty>.Success();
    }

    public async Task<OperationResult<List<Guid>>> GetGroupsAsync(Guid basePathId)
    {
        var basePath = await m_basePathRepository.GetAsync(basePathId);
        if (basePath is null)
        {
            return OperationResult<List<Guid>>.NotFound("Base path not found");
        }

        return OperationResult<List<Guid>>.Success(await m_basePathRepository.GetGroupIdsAsync(basePathId));
    }

    public async Task<OperationResult<Empty>> SetGroupsAsync(Guid basePathId, SetBasePathGroupsDto dto)
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

        // A stale group id would fail on the foreign key at save time — same treatment as a stale
        // user id in SetUsersAsync.
        var groupIds = await m_groupRepository.FilterExistingIdsAsync(dto.GroupIds ?? []);

        // Only the group grants are changing, so the direct grants are read as they stand: a user
        // who holds the base path in their own right keeps their links.
        var userIds = await m_basePathRepository.GetUserIdsAsync(basePathId);
        var revoked = await m_shareRepository.DeleteSharesLosingBasePathAccessAsync(basePathId, userIds, groupIds);

        await m_basePathRepository.ReplaceGroupAccessForBasePathAsync(basePathId, groupIds);
        await m_basePathRepository.SaveChangesAsync();

        m_logger.LogInformation(
            "{Actor:l} granted base path \"{Name:l}\" ({BasePathId}) to {GroupCount} group(s); "
            + "{ShareCount} link(s) revoked with it",
            m_auditActor.Describe(), basePath.Name, basePathId, groupIds.Count, revoked);
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

        // The account has to still be there. Every other grant screen resolves the row it is named
        // for before it writes — SetUsersAsync and SetGroupsAsync both 404 an unknown base path —
        // and this one did not, so an admin saving against a row for an account deleted meanwhile
        // reached SQLite and came back as an unhandled "FOREIGN KEY constraint failed": a 500, with
        // the whole grant change lost, where the same staleness anywhere else is a clean answer.
        var existing = await m_basePathRepository.FilterExistingUserIdsAsync([userId]);
        if (existing.Count == 0)
        {
            return OperationResult<Empty>.NotFound("User not found");
        }

        var requested = dto.BasePathIds ?? [];

        // An id that matches no base path would fail on the foreign key at save time; drop it here
        // so a stale id in the admin UI does not take the whole grant list down with it.
        var basePathIds = await m_basePathRepository.FilterExistingIdsAsync(requested);

        // Same revocation as SetUsersAsync, from the other end of the same table — both screens
        // edit the same grants, so both have to take the links with them. A base path the user
        // still reaches through a group, or through the Admin role, is not a revocation.
        var revoked = await m_shareRepository.DeleteSharesOfUserLosingAccessAsync(userId, basePathIds);

        await m_basePathRepository.ReplaceAccessForUserAsync(userId, basePathIds);
        await m_basePathRepository.SaveChangesAsync();

        m_logger.LogInformation(
            "{Actor:l} granted user {UserId} {Count} base path(s); {ShareCount} link(s) revoked with it",
            m_auditActor.Describe(), userId, basePathIds.Count, revoked);
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

    private static BasePathDto MapBasePath(BasePath basePath, int userCount, int groupCount) => new()
    {
        Id = basePath.Id,
        Path = basePath.Path,
        Name = basePath.Name,
        CreatedAt = basePath.CreatedAt,
        UserCount = userCount,
        GroupCount = groupCount
    };
}
