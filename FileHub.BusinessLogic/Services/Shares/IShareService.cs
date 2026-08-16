using Dtos.Shares;
using Shared;

namespace FileHub.BusinessLogic.Services.Shares;

/// <summary>
/// Public download links. Creating and listing one is an authenticated, grant-checked operation;
/// redeeming one is not, which is why <see cref="ResolvePublicAsync"/> does as little work as it
/// possibly can.
/// </summary>
public interface IShareService
{
    /// <summary>
    /// Creates a link to a path the caller has been granted. The target's total size is measured
    /// here, once, and cached on the row.
    /// </summary>
    Task<OperationResult<ShareDto>> CreateAsync(Guid userId, CreateShareDto dto);

    Task<OperationResult<List<ShareDto>>> ListForUserAsync(Guid userId);

    /// <summary>Every link in the install, for the admin area.</summary>
    Task<OperationResult<List<AdminShareDto>>> ListAllAsync();

    /// <summary>A user may revoke their own link; an admin may revoke any.</summary>
    Task<OperationResult<Empty>> DeleteAsync(Guid callerId, bool callerIsAdmin, Guid shareId);

    /// <summary>
    /// Resolves a link for an anonymous caller. Unknown id, exhausted download limit and a target
    /// that is no longer on disk all answer the same failure, so the response says nothing about
    /// which links exist. Re-resolves through the sandbox every time, and never walks the tree.
    /// </summary>
    Task<OperationResult<ResolvedShare>> ResolvePublicAsync(Guid shareId);

    /// <summary>Counts one redemption of a link against its <c>MaxDownloadCount</c>.</summary>
    Task<OperationResult<Empty>> RegisterDownloadAsync(Guid shareId);
}
