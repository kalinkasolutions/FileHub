namespace Dtos.Shares;

/// <summary>A link as its creator sees it. The absolute path on the host is deliberately not on it.</summary>
public sealed class ShareDto
{
    public Guid Id { get; set; }

    /// <summary>Name of the shared file or directory.</summary>
    public string Name { get; set; }

    public Guid BasePathId { get; set; }

    /// <summary>Path below the base path, so the owner can tell two links to the same name apart.</summary>
    public string RelativePath { get; set; }

    public bool IsDir { get; set; }

    /// <summary>Total bytes, measured when the link was created.</summary>
    public long Size { get; set; }

    public int DownloadCount { get; set; }

    /// <summary>0 means unlimited.</summary>
    public int MaxDownloadCount { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Null means anyone with the link; set means only members of that group.</summary>
    public Guid? AudienceGroupId { get; set; }

    /// <summary>The audience group's name, so the UI can label the link without a second call.</summary>
    public string AudienceGroupName { get; set; }

    /// <summary>The absolute public URL; stamped by the API layer, which owns the app's base URL.</summary>
    public string Link { get; set; }
}
