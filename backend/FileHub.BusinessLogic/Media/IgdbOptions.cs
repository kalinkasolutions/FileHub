namespace FileHub.BusinessLogic.Media;

/// <summary>The <c>Igdb</c> configuration section: a Twitch developer application's client id and
/// secret, which IGDB's API is reached with.</summary>
public sealed class IgdbOptions
{
    public const string SectionName = "Igdb";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>The v4 API; every query is relative to it.</summary>
    public string ApiUrl { get; set; } = "https://api.igdb.com/v4/";

    /// <summary>Twitch's id service, where the bearer token for the API is bought.</summary>
    public string TokenUrl { get; set; } = "https://id.twitch.tv/";

    /// <summary>Where a cover's image id is fetched from, at the size the popover shows.</summary>
    public string ImageUrl { get; set; } = "https://images.igdb.com/igdb/image/upload/t_cover_big";
}
