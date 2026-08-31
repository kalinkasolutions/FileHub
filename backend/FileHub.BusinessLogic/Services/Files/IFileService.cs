using Dtos.Files;
using Shared;

namespace FileHub.BusinessLogic.Services.Files;

/// <summary>
/// Browsing and downloading, always as one specific user. Every method starts from what that user
/// can reach: their own <c>BasePathAccess</c> rows, the base paths granted to the groups they
/// belong to, and — when <c>callerIsAdmin</c> — every base path there is. Anything else does not
/// exist as far as they are concerned, and every path below one is resolved through
/// <c>PathSandbox</c>.
/// <para>
/// <c>callerIsAdmin</c> is threaded down from the endpoint (<c>ClaimsPrincipal.IsInRole</c>) rather
/// than resolved here, so it is visible at every layer what grants the access.
/// </para>
/// </summary>
public interface IFileService
{
    /// <summary>The base paths the caller can reach, as listing entries — the root of the file browser.</summary>
    Task<OperationResult<List<FileEntryDto>>> GetBasePathsAsync(Guid userId, bool callerIsAdmin);

    Task<OperationResult<NavigationDto>> NavigateAsync(Guid userId, bool callerIsAdmin, NavigateDto dto);

    /// <summary>The validated absolute path for the endpoint to stream, or a failure to answer with.</summary>
    Task<OperationResult<ResolvedFile>> ResolveDownloadAsync(
        Guid userId, bool callerIsAdmin, Guid basePathId, string relativePath);
}
