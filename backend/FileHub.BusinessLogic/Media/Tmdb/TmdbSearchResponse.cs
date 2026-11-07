namespace FileHub.BusinessLogic.Media.Tmdb;

/// <summary>The page <c>search/multi</c> answers with.</summary>
public sealed record TmdbSearchResponse
{
    public List<TmdbSearchResult>? Results { get; init; }
}
