namespace Dtos.Media;

/// <summary>What the info popover shows for one folder. <see cref="Found"/> false means the source
/// was asked and had nothing; <see cref="Query"/> is then the title it was asked for.</summary>
public sealed class MediaInfoDto
{
    public Guid Id { get; set; }

    public bool Found { get; set; }

    public string Query { get; set; }

    /// <summary>"tmdb" or "igdb".</summary>
    public string Source { get; set; }

    /// <summary>"movie", "tv" or "game"; empty when nothing was found.</summary>
    public string MediaType { get; set; }

    public string Title { get; set; }

    public int? Year { get; set; }

    public string Overview { get; set; }

    /// <summary>0–10, or null when the source has no votes for it.</summary>
    public double? Rating { get; set; }

    public List<string> Genres { get; set; } = [];

    public List<string> Platforms { get; set; } = [];

    /// <summary>The poster is at <c>api/media/poster/{Id}</c> when this is true.</summary>
    public bool HasPoster { get; set; }

    public string ExternalUrl { get; set; }

    public DateTime FetchedAt { get; set; }
}
