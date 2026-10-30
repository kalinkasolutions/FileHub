namespace FileHub.BusinessLogic.Media;

/// <summary>One answer from a source, before it is stored. <see cref="PosterUrl"/> is the remote
/// image, which the service downloads once and never links to.</summary>
public sealed record MediaLookup(
    string MediaType,
    long ExternalId,
    string ExternalUrl,
    string Title,
    int? Year,
    string Overview,
    double? Rating,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Platforms,
    string? PosterUrl);
