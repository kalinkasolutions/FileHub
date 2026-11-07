namespace FileHub.BusinessLogic.Media.Tmdb;

/// <summary>A movie's or a show's own page (<c>movie/{id}</c>, <c>tv/{id}</c>). A movie has a
/// <see cref="Title"/> and a <see cref="ReleaseDate"/>, a show a <see cref="Name"/> and a
/// <see cref="FirstAirDate"/>; the other pair is absent.</summary>
public sealed record TmdbTitle
{
    public long Id { get; init; }

    public string? Title { get; init; }

    public string? Name { get; init; }

    public string? Overview { get; init; }

    public double? VoteAverage { get; init; }

    public string? PosterPath { get; init; }

    public string? ReleaseDate { get; init; }

    public string? FirstAirDate { get; init; }

    public List<TmdbGenre>? Genres { get; init; }

    public string DisplayTitle => Title ?? Name ?? string.Empty;

    public int? Year => TmdbDate.Year(ReleaseDate ?? FirstAirDate);
}
