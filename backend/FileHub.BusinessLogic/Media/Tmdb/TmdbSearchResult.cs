namespace FileHub.BusinessLogic.Media.Tmdb;

/// <summary>One hit of a multi search. <see cref="MediaType"/> is "movie", "tv" or "person"; a movie
/// dates itself by <see cref="ReleaseDate"/>, a show by <see cref="FirstAirDate"/>.</summary>
public sealed record TmdbSearchResult
{
    public long Id { get; init; }

    public string? MediaType { get; init; }

    public string? ReleaseDate { get; init; }

    public string? FirstAirDate { get; init; }

    public int? Year => TmdbDate.Year(ReleaseDate ?? FirstAirDate);
}
