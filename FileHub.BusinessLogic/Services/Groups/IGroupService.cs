using Dtos.Groups;
using Shared;

namespace FileHub.BusinessLogic.Services.Groups;

/// <summary>
/// A group is a named set of users that base paths can be granted to and shares can be aimed at.
/// Everything here except <see cref="ListForCallerAsync"/> is admin-only: groups are part of the
/// access model, so only the admin area edits them.
/// </summary>
public interface IGroupService
{
    Task<OperationResult<List<GroupDto>>> ListAsync();

    Task<OperationResult<GroupDto>> CreateAsync(SaveGroupDto dto);

    Task<OperationResult<GroupDto>> RenameAsync(Guid id, SaveGroupDto dto);

    /// <summary>
    /// Deletes a group. Its memberships and its base-path grants cascade, and so do the links aimed
    /// at it — a group share must never survive its group as an anonymous one. The links its
    /// members created under base paths they only reached through it are revoked too.
    /// </summary>
    Task<OperationResult<Empty>> DeleteAsync(Guid id);

    Task<OperationResult<List<Guid>>> GetMembersAsync(Guid id);

    /// <summary>Replaces the members; an id left out is removed, and loses what the group granted.</summary>
    Task<OperationResult<Empty>> SetMembersAsync(Guid id, SetGroupMembersDto dto);

    Task<OperationResult<List<Guid>>> GetBasePathsAsync(Guid id);

    /// <summary>Replaces the base paths granted to the group; an id left out is a revocation.</summary>
    Task<OperationResult<Empty>> SetBasePathsAsync(Guid id, SetGroupBasePathsDto dto);

    /// <summary>
    /// The groups the caller may aim a share at: the ones they belong to, or every group when they
    /// are an admin. The only route in this service an ordinary user reaches.
    /// </summary>
    Task<OperationResult<List<GroupSummaryDto>>> ListForCallerAsync(Guid userId, bool callerIsAdmin);
}
