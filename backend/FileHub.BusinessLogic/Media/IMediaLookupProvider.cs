using Entities.Paths;

namespace FileHub.BusinessLogic.Media;

/// <summary>
/// One source of titles: TMDb for <see cref="BasePathKind.Media"/>, IGDB for
/// <see cref="BasePathKind.Games"/>. The service picks the one whose <see cref="Kind"/> matches the
/// base path and never cares which it got.
/// </summary>
public interface IMediaLookupProvider
{
    BasePathKind Kind { get; }

    /// <summary>What to call it in a message: "TMDb", "IGDB".</summary>
    string Name { get; }

    /// <summary>What is stored on the row: "tmdb", "igdb".</summary>
    string Source { get; }

    bool IsConfigured { get; }

    /// <summary>Which settings are missing, for the message when <see cref="IsConfigured"/> is false.</summary>
    string NotConfiguredHint { get; }

    /// <summary>The best match, or null when the source has nothing for it. Throws for a source
    /// that could not be asked, which the caller does not cache.</summary>
    Task<MediaLookup?> LookupAsync(ParsedTitle title, CancellationToken cancellationToken);
}
