using Dtos.Files;
using Shared;

namespace FileHub.BusinessLogic.Services.Files;

/// <summary>
/// Browsing and downloading, always as one specific user. Every method starts from the caller's
/// grants: a base path they hold no <c>BasePathAccess</c> row for does not exist as far as they are
/// concerned, and every path below one is resolved through <c>PathSandbox</c>.
/// </summary>
public interface IFileService
{
    /// <summary>The caller's granted base paths, as listing entries — the root of the file browser.</summary>
    Task<OperationResult<List<FileEntryDto>>> GetBasePathsAsync(Guid userId);

    Task<OperationResult<NavigationDto>> NavigateAsync(Guid userId, NavigateDto dto);

    /// <summary>The validated absolute path for the endpoint to stream, or a failure to answer with.</summary>
    Task<OperationResult<ResolvedFile>> ResolveDownloadAsync(Guid userId, Guid basePathId, string relativePath);
}
