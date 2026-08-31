namespace Dtos.Shares;

/// <summary>
/// A link that was aimed at one of the caller's groups, as a member of that group sees it. Only an
/// admin can create one (<c>ShareService.CreateAsync</c>), so this is the receiving end of an
/// admin's decision, not of anything a peer did.
/// <para>
/// Deliberately thinner than <see cref="ShareDto"/>: no base path id and no relative path. A member
/// of the audience may hold no grant on the base path the file sits in, so naming the directories
/// above it would hand them the shape of a disk they cannot browse. The name of the target is what
/// the link is worth to them.
/// </para>
/// </summary>
public sealed class ReceivedShareDto
{
    public Guid Id { get; set; }

    /// <summary>Name of the shared file or directory.</summary>
    public string Name { get; set; }

    public bool IsDir { get; set; }

    /// <summary>Total bytes, measured when the link was created.</summary>
    public long Size { get; set; }

    public int DownloadCount { get; set; }

    /// <summary>0 means unlimited. Shown because a member arriving at a spent link would otherwise
    /// only find out by following it and being told the link does not exist.</summary>
    public int MaxDownloadCount { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Which of the caller's groups this was aimed at — never null here, by definition.</summary>
    public Guid AudienceGroupId { get; set; }

    public string AudienceGroupName { get; set; }

    /// <summary>Who shared it, by display name, falling back to their address.</summary>
    public string SharedBy { get; set; }

    /// <summary>The absolute public URL; stamped by the API layer, which owns the app's base URL.</summary>
    public string Link { get; set; }
}
