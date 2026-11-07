using Entities.Media;

namespace FileHub.BusinessLogic.Media;

/// <summary>When a cached look-up has to be asked again.</summary>
public static class MediaInfoAge
{
    /// <summary>TMDb's terms allow a cached answer for six months and no longer; IGDB's do not
    /// mind, but one rule is simpler than two and a game's rating moves too.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(180);

    public static bool IsStale(MediaInfo info, DateTime now) => now - info.FetchedAt >= MaxAge;

    /// <summary>
    /// Whether a share link's preview should queue a look-up. A miss never does, however old: a
    /// stranger opening a link must not be able to make the same failing search run again — an
    /// admin's refresh is the way to retry one.
    /// </summary>
    public static bool NeedsRefreshForLink(MediaInfo? info, DateTime now) =>
        info is null || (info.Found && IsStale(info, now));
}
