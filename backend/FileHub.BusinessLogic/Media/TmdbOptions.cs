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
}
