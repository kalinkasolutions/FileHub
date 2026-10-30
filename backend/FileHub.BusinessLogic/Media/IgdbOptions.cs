namespace FileHub.BusinessLogic.Media;

/// <summary>The <c>Igdb</c> configuration section: a Twitch developer application's client id and
/// secret, which IGDB's API is reached with.</summary>
public sealed class IgdbOptions
{
    public const string SectionName = "Igdb";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;
}
