namespace FileHub.BusinessLogic.Media;

/// <summary>
/// What a folder name says once the release noise is taken off it. An id tag in the name
/// (<c>{tmdb-4556}</c>, <c>{tmdb-tv-295778}</c>, <c>{igdb-1020}</c>) pins the match outright,
/// which is how a wrong guess is corrected: rename the folder.
/// <para>
/// <see cref="TmdbType"/> is "movie" or "tv" when the tag says which, null when it does not. TMDb
/// ids are only unique per type — movie 295778 and show 295778 are different titles — so a bare tag
/// is settled by whichever of the two carries the folder's title.
/// </para>
/// </summary>
public sealed record ParsedTitle(string Title, int? Year, long? TmdbId, string? TmdbType, long? IgdbId);
