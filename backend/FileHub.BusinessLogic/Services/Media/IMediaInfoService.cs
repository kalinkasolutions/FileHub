using Dtos.Media;
using Shared;

namespace FileHub.BusinessLogic.Services.Media;

/// <summary>
/// The info behind a folder's info button: what TMDb or IGDB knows about it, looked up on the first
/// ask and cached in the database from then on. Every method starts from the same question the
/// browser does — can this user reach this base path — and answers "not found" when they cannot.
/// </summary>
public interface IMediaInfoService
{
    /// <summary>
    /// The cached answer for one folder directly under a base path, fetching it from the source when
    /// there is none, when it is older than six months, or when <paramref name="refresh"/> says so.
    /// <paramref name="refresh"/> is admin-only on the route; the service takes it as a plain flag.
    /// </summary>
    Task<OperationResult<MediaInfoDto>> GetAsync(
        Guid userId, bool callerIsAdmin, Guid basePathId, string relativePath, bool refresh,
        CancellationToken cancellationToken);

    /// <summary>The absolute path of a stored poster, for the endpoint to stream. Not found when
    /// the row, the poster, or the caller's access to its base path is missing.</summary>
    Task<OperationResult<string>> GetPosterPathAsync(Guid userId, bool callerIsAdmin, Guid mediaInfoId);
}
