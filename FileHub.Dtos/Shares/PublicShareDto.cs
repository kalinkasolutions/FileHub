namespace Dtos.Shares;

/// <summary>
/// What the public share landing page renders. Everything else about the link — who made it, where
/// it points on the host, how often it has been fetched — stays behind the cookie.
/// </summary>
public sealed class PublicShareDto
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    /// <summary>Total bytes, read from the row. The public routes never walk the tree.</summary>
    public long Size { get; set; }

    public bool IsDir { get; set; }
}
