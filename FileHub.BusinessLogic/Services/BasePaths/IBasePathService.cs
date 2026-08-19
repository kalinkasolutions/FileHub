using Dtos.BasePaths;
using Shared;

namespace FileHub.BusinessLogic.Services.BasePaths;

/// <summary>
/// Admin-only administration of the directories FileHub is allowed to read, and of who may see
/// them. Nothing here is reachable by a browsing user: their view goes through <c>IFileService</c>,
/// which never learns a path it was not granted.
/// </summary>
public interface IBasePathService
{
    Task<OperationResult<List<BasePathDto>>> GetAllAsync();

    Task<OperationResult<BasePathDto>> CreateAsync(SaveBasePathDto dto);

    Task<OperationResult<BasePathDto>> UpdateAsync(Guid id, SaveBasePathDto dto);

    Task<OperationResult<Empty>> DeleteAsync(Guid id);

    /// <summary>The users granted this base path directly.</summary>
    Task<OperationResult<List<Guid>>> GetUsersAsync(Guid basePathId);

    /// <summary>Replaces the users granted this base path directly.</summary>
    Task<OperationResult<Empty>> SetUsersAsync(Guid basePathId, SetBasePathAccessDto dto);

    /// <summary>The groups granted this base path — every member of one reaches it.</summary>
    Task<OperationResult<List<Guid>>> GetGroupsAsync(Guid basePathId);

    /// <summary>Replaces the groups granted this base path.</summary>
    Task<OperationResult<Empty>> SetGroupsAsync(Guid basePathId, SetBasePathGroupsDto dto);

    /// <summary>The base paths one user is granted.</summary>
    Task<OperationResult<List<Guid>>> GetUserBasePathsAsync(Guid userId);

    /// <summary>Replaces the base paths one user is granted.</summary>
    Task<OperationResult<Empty>> SetUserBasePathsAsync(Guid userId, SetUserBasePathsDto dto);
}
