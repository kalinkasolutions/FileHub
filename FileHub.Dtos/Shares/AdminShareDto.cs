namespace Dtos.Shares;

/// <summary>
/// A link as an admin sees it: the same fields as <see cref="ShareDto"/> plus who created it and
/// which base path it lives under, since the admin list spans every user.
/// </summary>
public sealed class AdminShareDto
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    public Guid BasePathId { get; set; }

    public string BasePathName { get; set; }

    public string RelativePath { get; set; }

    public bool IsDir { get; set; }

    public long Size { get; set; }

    public int DownloadCount { get; set; }

    public int MaxDownloadCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CreatedById { get; set; }

    public string CreatedBy { get; set; }

    public string Link { get; set; }
}
