using FileHub.BusinessLogic;

namespace FileHub.Links;

/// <summary>
/// The absolute URLs the app hands out, all built from one configured origin
/// (<see cref="AppOptions.BaseUrl"/>). This replaces the Go <c>links</c> package, which assembled
/// them from a protocol flag and a domain — two settings that could disagree.
/// <para>
/// It lives in the API layer because a URL is a transport concern: the services return ids and let
/// the endpoint stamp the link, so nothing below the API has to know the app's public address.
/// </para>
/// </summary>
public static class ShareLinks
{
    /// <summary>
    /// The link a user copies. It points at the Open Graph page rather than straight at the landing
    /// page, so a chat client that unfurls it gets a title, a size and a preview image.
    /// </summary>
    public static string Share(AppOptions options, Guid shareId) =>
        $"{options.TrimmedBaseUrl()}/og/share/{shareId}";

    /// <summary>The SPA route that actually renders the share.</summary>
    public static string Landing(AppOptions options, Guid shareId) =>
        $"{options.TrimmedBaseUrl()}/share/{shareId}";

    /// <summary>Where a dead or exhausted link sends a browser.</summary>
    public static string NotFound(AppOptions options) =>
        $"{options.TrimmedBaseUrl()}/404";

    /// <summary>The preview image the Open Graph page advertises.</summary>
    public static string PreviewImage(AppOptions options) =>
        $"{options.TrimmedBaseUrl()}/filehub.png";
}
