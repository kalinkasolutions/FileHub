namespace FileHub.BusinessLogic.Media.Tmdb;

/// <summary>TMDb dates are "2024-02-27" strings, and an unreleased title's is empty.</summary>
public static class TmdbDate
{
    public static int? Year(string? date) =>
        date is { Length: >= 4 } && int.TryParse(date.AsSpan(0, 4), out var year) ? year : null;
}
