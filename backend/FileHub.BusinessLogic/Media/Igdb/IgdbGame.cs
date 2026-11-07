namespace FileHub.BusinessLogic.Media.Igdb;

/// <summary>One game as the <c>games</c> query answers it, with the fields the query asked for.
/// <see cref="FirstReleaseDate"/> is a Unix timestamp; <see cref="TotalRating"/> is out of 100.</summary>
public sealed record IgdbGame
{
    public long Id { get; init; }

    public string? Name { get; init; }

    public string? Url { get; init; }

    public string? Summary { get; init; }

    public long? FirstReleaseDate { get; init; }

    public double? TotalRating { get; init; }

    public IgdbCover? Cover { get; init; }

    public List<IgdbGenre>? Genres { get; init; }

    public List<IgdbPlatform>? Platforms { get; init; }

    public int? Year => FirstReleaseDate is { } seconds ? DateTimeOffset.FromUnixTimeSeconds(seconds).Year : null;
}
