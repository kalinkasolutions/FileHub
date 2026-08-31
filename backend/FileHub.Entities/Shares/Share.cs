using Entities.Account;
using Entities.Groups;
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

    /// <summary>
    /// Who the link is for. Null is the default and means anonymous-by-URL: anyone holding the URL
    /// gets the file. Set, the link only answers a signed-in member of that group (or an admin).
    /// <para>
    /// The foreign key cascades on purpose: deleting the group deletes the links aimed at it. If it
    /// only nulled the column, deleting a group would silently turn every link it gated into an
    /// anonymous one — a privilege escalation nobody performed. That must not depend on a service
    /// remembering to clean up.
    /// </para>
    /// </summary>
    public Guid? AudienceGroupId { get; set; }
    public Group AudienceGroup { get; set; }

    public Guid CreatedById { get; set; }
    public FileHubUser CreatedBy { get; set; }

    /// <summary>A <see cref="MaxDownloadCount"/> of 0 means unlimited, so it is never reached.</summary>
    public bool DownloadLimitReached => MaxDownloadCount > 0 && DownloadCount >= MaxDownloadCount;
}
