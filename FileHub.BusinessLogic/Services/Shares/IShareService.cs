using Dtos.Shares;
using Shared;

namespace FileHub.BusinessLogic.Services.Shares;

/// <summary>
/// Download links. Creating and listing one is an authenticated, access-checked operation;
/// redeeming one usually is not, which is why <see cref="ResolvePublicAsync"/> does as little work
/// as it possibly can.
/// <para>
/// A link either has no audience — anonymous by URL, the default — or is aimed at a group, in which
/// case only a signed-in member of it (or an admin) may redeem it. The public routes therefore take
/// the caller as a nullable id: null is the anonymous case, and it is the cheap one.
/// </para>
/// </summary>
public interface IShareService
{
    /// <summary>
    /// Creates a link to a path the caller can reach. The target's total size is measured here,
    /// once, and cached on the row. An audience group the caller does not belong to is a 400 —
    /// unless they are an admin, who may aim a link at any group.
    /// <para>
    /// <paramref name="callerCanCreateShares"/> is the <c>CreateShares</c> role, which publishing
    /// requires: reading a disk and handing out an anonymous URL into it are separate powers, and
    /// access to the base path only settles the first. An admin holds the role implicitly, so
    /// <paramref name="callerIsAdmin"/> is enough on its own.
    /// </para>
    /// </summary>
    Task<OperationResult<ShareDto>> CreateAsync(
        Guid userId, bool callerIsAdmin, bool callerCanCreateShares, CreateShareDto dto);

    Task<OperationResult<List<ShareDto>>> ListForUserAsync(Guid userId);

    /// <summary>Every link in the install, for the admin area.</summary>
    Task<OperationResult<List<AdminShareDto>>> ListAllAsync();

    /// <summary>A user may revoke their own link; an admin may revoke any.</summary>
    Task<OperationResult<Empty>> DeleteAsync(Guid callerId, bool callerIsAdmin, Guid shareId);

    /// <summary>
    /// Resolves a link for a public caller, who may or may not be signed in. Unknown id, exhausted
    /// download limit, a target that is no longer on disk and an audience the caller is not in all
    /// answer the same failure, so the response says nothing about which links exist or who they
    /// are for. Re-resolves through the sandbox every time, and never walks the tree.
    /// </summary>
    Task<OperationResult<ResolvedShare>> ResolvePublicAsync(Guid shareId, Guid? callerId, bool callerIsAdmin);

    /// <summary>
    /// Claims one redemption of a link against its <c>MaxDownloadCount</c> and its audience. This is
    /// where both are enforced: the increment is conditional in the database, so a failure means the
    /// link is unknown, already spent, or not for this caller, and they must not be given the file.
    /// Answers the same <c>NotFound</c> as every other public failure.
    /// </summary>
    Task<OperationResult<Empty>> RegisterDownloadAsync(Guid shareId, Guid? callerId, bool callerIsAdmin);
}
