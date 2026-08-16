using Entities.Account;
using Entities.Paths;

namespace Entities.Shares;

/// <summary>
/// A public download link for one file or directory. The target is stored as
/// (base path, relative path) rather than as a resolved absolute path, so a share can never
/// outlive its base path and can never point outside it — it is re-resolved through the same
/// sandbox as a browsing request on every hit.
/// </summary>
public sealed class Share : IBaseEntity
{
    /// <summary>Also the share token: this id is what appears in the public link.</summary>
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }

    public Guid BasePathId { get; set; }
    public BasePath BasePath { get; set; }

    /// <summary>Path below the base path, empty when the share points at the base path itself.</summary>
    public string RelativePath { get; set; }

    public int DownloadCount { get; set; }

    /// <summary>0 means unlimited.</summary>
    public int MaxDownloadCount { get; set; }

    /// <summary>Total bytes, measured once when the share is created (see <c>ShareService</c>).</summary>
    public long Size { get; set; }

    public Guid CreatedById { get; set; }
    public FileHubUser CreatedBy { get; set; }

    /// <summary>A <see cref="MaxDownloadCount"/> of 0 means unlimited, so it is never reached.</summary>
    public bool DownloadLimitReached => MaxDownloadCount > 0 && DownloadCount >= MaxDownloadCount;
}
