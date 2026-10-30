using Entities.Paths;

namespace Entities.Media;

/// <summary>
/// What TMDb or IGDB said about one folder directly under a base path — the cached answer behind
/// the file browser's info button. One row per (base path, folder), written on the first look-up
/// and read ever after, so a folder costs the API one request rather than one per visitor.
/// <para>
/// A folder nothing matched is a row too (<see cref="Found"/> false). Remembering the miss is what
/// keeps the same failing search from running again on every open.
/// </para>
/// </summary>
public sealed class MediaInfo : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }

    public Guid BasePathId { get; set; }
    public BasePath BasePath { get; set; }

    /// <summary>The folder's name, which is its whole path below the base path.</summary>
    public string RelativePath { get; set; }

    /// <summary>The title the folder name was reduced to before searching; shown when nothing matched.</summary>
    public string Query { get; set; }

    public bool Found { get; set; }

    /// <summary>"tmdb" or "igdb".</summary>
    public string Source { get; set; }

    /// <summary>"movie", "tv" or "game"; empty when nothing was found.</summary>
    public string MediaType { get; set; }

    public long ExternalId { get; set; }

    /// <summary>The title's own page at the source, for the "via TMDb" link.</summary>
    public string ExternalUrl { get; set; }

    public string Title { get; set; }

    public int? Year { get; set; }

    public string Overview { get; set; }

    /// <summary>0–10; null when the source has no votes for it.</summary>
    public double? Rating { get; set; }

    /// <summary>Comma separated, for display only — never queried.</summary>
    public string Genres { get; set; }

    /// <summary>Comma separated; games only.</summary>
    public string Platforms { get; set; }

    /// <summary>Whether a poster file is stored under this row's id.</summary>
    public bool HasPoster { get; set; }

    /// <summary>When the source last answered. TMDb's terms cap a cached answer at six months.</summary>
    public DateTime FetchedAt { get; set; }
}
