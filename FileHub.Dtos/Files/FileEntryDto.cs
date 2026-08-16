namespace Dtos.Files;

/// <summary>
/// One row of a listing — a base path, a directory or a file. The shape is the Go build's
/// <c>PublicPath</c>, which the SPA's navigation is written against.
/// </summary>
public sealed class FileEntryDto
{
    /// <summary>The base path this entry lives under, not an id of the entry itself. It is what the
    /// client sends back to navigate or download, together with <see cref="NextSegment"/>.</summary>
    public Guid Id { get; set; }

    public string Name { get; set; }

    public bool IsDir { get; set; }

    /// <summary>Bytes for a file; the number of entries it holds for a directory.</summary>
    public long Size { get; set; }

    /// <summary>Path below the base path, without a leading separator. Empty for a base path.</summary>
    public string NextSegment { get; set; }

    public bool IsBasePath { get; set; }

    /// <summary>
    /// A fresh id per listing, for identity comparisons <em>within</em> one response only. It is
    /// regenerated every time, so it must never be persisted or compared across requests.
    /// </summary>
    public Guid ItemId { get; set; }
}
