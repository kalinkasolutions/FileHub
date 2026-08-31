namespace FileHub.BusinessLogic;

/// <summary>
/// Whole-app settings bound from the <c>App</c> configuration section. This replaces the Go
/// build's <c>Domain</c>/<c>Ssl</c> pair: one absolute origin, used both for the links in mails
/// and for the share links the API hands out, so the two can never disagree.
/// </summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Public origin of the app, no trailing slash, e.g. "https://file-hub.example.com".</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string TrimmedBaseUrl() => BaseUrl.TrimEnd('/');
}
