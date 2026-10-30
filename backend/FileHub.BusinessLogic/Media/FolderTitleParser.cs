using System.Text.RegularExpressions;

namespace FileHub.BusinessLogic.Media;

/// <summary>
/// Reduces a folder name to the title a search can find. Pure: the same name always parses the
/// same way, and nothing here touches the disk or the network.
/// <para>
/// "Dune.Part.Two.2024.1080p.BluRay.x264-GROUP" is "Dune Part Two" from 2024; "The Matrix (1999)"
/// is "The Matrix" from 1999; "Cyberpunk 2077 [FitGirl Repack]" is "Cyberpunk 2077" with no year,
/// because 2077 is not one. The first release tag ends the title, as does a bare year that is not
/// the first word — "2001 A Space Odyssey" keeps its 2001.
/// </para>
/// </summary>
public static partial class FolderTitleParser
{
    // A token that is one of these, or that ends in "-<one of these>" the way a release group is
    // written, ends the title. Lower case; compared ignoring case.
    private static readonly HashSet<string> ReleaseTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "480p", "720p", "1080p", "1080i", "2160p", "4k", "uhd", "hdr", "hdr10", "dv", "sdr",
        "x264", "x265", "h264", "h265", "hevc", "av1", "xvid", "divx",
        "bluray", "blu-ray", "bdrip", "brrip", "bdremux", "remux", "web-dl", "webdl", "webrip",
        "hdtv", "dvdrip", "dvd", "hdrip", "camrip", "ts", "tc",
        "repack", "proper", "internal", "complete", "extended", "unrated", "remastered", "multi",
        "dl", "ac3", "dts", "aac", "atmos", "truehd", "eac3", "german", "english", "dubbed", "subbed",
        "gog", "fitgirl", "codex", "rune", "dodi", "plaza", "skidrow", "elamigos", "tenoke",
        "razor1911", "empress", "flt", "build", "update", "dlc", "incl", "repacks"
    };

    public static ParsedTitle Parse(string folderName)
    {
        var tmdb = IdTag(folderName, "tmdb");
        var igdb = IdTag(folderName, "igdb");

        var text = CurlyTag().Replace(folderName, " ");

        // "(1999)" is a year by convention; any other bracketed note is a release detail.
        int? year = null;
        text = BracketedYear().Replace(text, match =>
        {
            year ??= AsYear(match.Groups["year"].Value);
            return " ";
        });
        text = Bracketed().Replace(text, " ");

        var title = new List<string>();

        foreach (var raw in Separator().Split(text))
        {
            if (raw.Length == 0)
            {
                continue;
            }

            var token = raw;
            var endsHere = false;

            var dash = token.LastIndexOf('-');
            if (dash > 0 && ReleaseTags.Contains(token[(dash + 1)..]))
            {
                token = token[..dash];
                endsHere = true;
            }

            if (ReleaseTags.Contains(token) || Version().IsMatch(token) || Episode().IsMatch(token))
            {
                break;
            }

            if (year is null && title.Count > 0 && AsYear(token) is { } bareYear)
            {
                year = bareYear;
                break;
            }

            title.Add(token);

            if (endsHere)
            {
                break;
            }
        }

        var joined = string.Join(' ', title).Trim(' ', '-', '.');

        // Never search for nothing: a name made only of tags is searched for as it is.
        return new ParsedTitle(joined.Length > 0 ? joined : folderName.Trim(), year, tmdb.Id, tmdb.Type, igdb.Id);
    }

    private static (long? Id, string? Type) IdTag(string folderName, string source)
    {
        var match = IdTagPattern().Match(folderName);

        while (match.Success)
        {
            if (string.Equals(match.Groups["source"].Value, source, StringComparison.OrdinalIgnoreCase)
                && long.TryParse(match.Groups["id"].Value, out var id))
            {
                var type = match.Groups["type"].Success ? match.Groups["type"].Value.ToLowerInvariant() : null;
                return (id, type);
            }

            match = match.NextMatch();
        }

        return (null, null);
    }

    /// <summary>A plausible release year, which "2077" and "1080" are not.</summary>
    private static int? AsYear(string token)
    {
        if (token.Length != 4 || !int.TryParse(token, out var year))
        {
            return null;
        }

        return year >= 1900 && year <= DateTime.UtcNow.Year + 1 ? year : null;
    }

    // A folder name is short and the patterns are linear, but the analyzer wants the timeout said.
    private const int MatchTimeout = 1000;

    [GeneratedRegex(@"\{(?<source>[a-z]+)-(?:(?<type>movie|tv)-)?(?<id>\d+)\}", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, MatchTimeout)]
    private static partial Regex IdTagPattern();

    [GeneratedRegex(@"\{[^}]*\}", RegexOptions.None, MatchTimeout)]
    private static partial Regex CurlyTag();

    [GeneratedRegex(@"\((?<year>\d{4})\)", RegexOptions.ExplicitCapture, MatchTimeout)]
    private static partial Regex BracketedYear();

    [GeneratedRegex(@"[\[\(][^\]\)]*[\]\)]", RegexOptions.None, MatchTimeout)]
    private static partial Regex Bracketed();

    [GeneratedRegex(@"[.\s_]+", RegexOptions.None, MatchTimeout)]
    private static partial Regex Separator();

    [GeneratedRegex(@"^v\d+(?:\.\d+)*[a-z]?$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, MatchTimeout)]
    private static partial Regex Version();

    [GeneratedRegex(@"^S\d{1,2}(?:E\d{1,3})?(?:-S?\d{1,2}(?:E\d{1,3})?)?$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, MatchTimeout)]
    private static partial Regex Episode();
}
