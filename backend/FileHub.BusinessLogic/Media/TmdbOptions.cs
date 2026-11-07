namespace FileHub.BusinessLogic.Media;

/// <summary>The <c>Tmdb</c> configuration section. Without a key, a media base path's info button
/// answers with a message saying so rather than failing.</summary>
public sealed class TmdbOptions
{
    public const string SectionName = "Tmdb";

    /// <summary>Either credential TMDb hands out: the short v3 API key, or the long "API Read Access
    /// Token" (a JWT starting with "eyJ"). Both are accepted.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The language titles and overviews come back in, as an IETF tag TMDb knows.</summary>
    public string Language { get; set; } = "en-US";

    /// <summary>The v3 API; every request is relative to it.</summary>
    public string ApiUrl { get; set; } = "https://api.themoviedb.org/3/";

    /// <summary>Where a poster path from the API is fetched from, at the width the popover shows.</summary>
    public string ImageUrl { get; set; } = "https://image.tmdb.org/t/p/w342";

    /// <summary>The site a title's own page is linked to.</summary>
    public string SiteUrl { get; set; } = "https://www.themoviedb.org";
}
