using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Entities.Paths;
using Microsoft.Extensions.Options;

namespace FileHub.BusinessLogic.Media;

/// <summary>
/// Movies and series from The Movie Database. Two calls per look-up: a multi search that says
/// whether the name is a movie or a show, then that title's own page for the genres and the
/// overview. An id tag in the folder name skips the search.
/// </summary>
public sealed class TmdbLookupProvider : IMediaLookupProvider
{
    private const string ImageBase = "https://image.tmdb.org/t/p/w342";
    private const string SiteBase = "https://www.themoviedb.org";

    private readonly HttpClient m_http;
    private readonly TmdbOptions m_options;

    public TmdbLookupProvider(HttpClient http, IOptions<TmdbOptions> options)
    {
        m_http = http;
        m_options = options.Value;
    }

    public BasePathKind Kind => BasePathKind.Media;
    public string Name => "TMDb";
    public string Source => "tmdb";
    public bool IsConfigured => Key.Length > 0;
    public string NotConfiguredHint => "set Tmdb__ApiKey";

    private string Key => (m_options.ApiKey ?? string.Empty).Trim().Trim('"', '\'');

    public async Task<MediaLookup?> LookupAsync(ParsedTitle title, CancellationToken cancellationToken)
    {
        if (title.TmdbId is { } id)
        {
            return await ByIdAsync(id, title, cancellationToken);
        }

        // A German release spells its umlauts out ("Zum Glueck"), and TMDb's search does not know
        // that spelling; the folded form is tried only once the written one came back empty.
        foreach (var query in Spellings(title.Title))
        {
            var hit = await SearchAsync(query, title.Year, cancellationToken);

            if (hit is not null)
            {
                return await DetailsAsync(hit.Value.Type, hit.Value.Id, cancellationToken);
            }
        }

        return null;
    }

    /// <summary>
    /// A tagged folder. TMDb numbers movies and shows separately, so a bare id can be both — movie
    /// 295778 is a wrestling compilation, show 295778 is Scrubs — and a tag without the type is
    /// settled by whichever carries the folder's own title, then its year, then the movie.
    /// </summary>
    private async Task<MediaLookup?> ByIdAsync(long id, ParsedTitle title, CancellationToken cancellationToken)
    {
        if (title.TmdbType is { } type)
        {
            return await DetailsAsync(type, id, cancellationToken);
        }

        var movie = await DetailsAsync("movie", id, cancellationToken);
        var show = await DetailsAsync("tv", id, cancellationToken);

        if (movie is null || show is null)
        {
            return movie ?? show;
        }

        var wanted = TitleKey.Of(title.Title);
        if (TitleKey.Of(show.Title) == wanted && TitleKey.Of(movie.Title) != wanted)
        {
            return show;
        }

        if (TitleKey.Of(movie.Title) == wanted && TitleKey.Of(show.Title) != wanted)
        {
            return movie;
        }

        if (title.Year is { } year && show.Year == year && movie.Year != year)
        {
            return show;
        }

        return movie;
    }

    private static IEnumerable<string> Spellings(string title)
    {
        var withUmlauts = title
            .Replace("ae", "ä").Replace("oe", "ö").Replace("ue", "ü")
            .Replace("Ae", "Ä").Replace("Oe", "Ö").Replace("Ue", "Ü");

        return withUmlauts == title ? [title] : [title, withUmlauts];
    }

    private async Task<(string Type, long Id)?> SearchAsync(string query, int? year, CancellationToken cancellationToken)
    {
        using var document = await GetAsync(
            "search/multi", [("query", query), ("include_adult", "false")], cancellationToken);

        if (document is null || !document.RootElement.TryGetProperty("results", out var results))
        {
            return null;
        }

        (string Type, long Id)? first = null;

        foreach (var result in results.EnumerateArray())
        {
            var type = result.GetProperty("media_type").GetString();
            if (type is not ("movie" or "tv"))
            {
                continue;
            }

            var candidate = (type, result.GetProperty("id").GetInt64());
            first ??= candidate;

            // The year is the one thing the folder name says that a search cannot filter on here, so
            // the first result from that year beats the first result overall.
            if (year is null || YearOf(result, type) == year)
            {
                return candidate;
            }
        }

        return first;
    }

    private async Task<MediaLookup?> DetailsAsync(string type, long id, CancellationToken cancellationToken)
    {
        using var document = await GetAsync($"{type}/{id}", [], cancellationToken);

        if (document is null)
        {
            return null;
        }

        var root = document.RootElement;
        var titleField = type == "movie" ? "title" : "name";
        var genres = root.TryGetProperty("genres", out var genreArray)
            ? genreArray.EnumerateArray().Select(g => g.GetProperty("name").GetString() ?? string.Empty).Where(g => g.Length > 0).ToList()
            : [];
        var rating = root.TryGetProperty("vote_average", out var vote) ? vote.GetDouble() : 0;
        var poster = root.TryGetProperty("poster_path", out var posterPath) ? posterPath.GetString() : null;

        return new MediaLookup(
            MediaType: type,
            ExternalId: id,
            ExternalUrl: $"{SiteBase}/{type}/{id}",
            Title: root.GetProperty(titleField).GetString() ?? string.Empty,
            Year: YearOf(root, type),
            Overview: root.TryGetProperty("overview", out var overview) ? overview.GetString() ?? string.Empty : string.Empty,
            Rating: rating > 0 ? Math.Round(rating, 1) : null,
            Genres: genres,
            Platforms: [],
            PosterUrl: string.IsNullOrEmpty(poster) ? null : ImageBase + poster);
    }

    private static int? YearOf(JsonElement element, string type)
    {
        var field = type == "movie" ? "release_date" : "first_air_date";
        var date = element.TryGetProperty(field, out var value) ? value.GetString() : null;

        return date is { Length: >= 4 } && int.TryParse(date.AsSpan(0, 4), out var year) ? year : null;
    }

    /// <summary>One GET against the v3 API; null for a 404, an exception for anything else wrong.</summary>
    private async Task<JsonDocument?> GetAsync(
        string path, IEnumerable<(string Name, string Value)> query, CancellationToken cancellationToken)
    {
        var parameters = new List<(string Name, string Value)>(query) { ("language", m_options.Language) };
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        // TMDb hands out two credentials: the short v3 key goes in the query, the long read access
        // token (a JWT) goes in the Authorization header.
        if (Key.StartsWith("eyJ", StringComparison.Ordinal))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Key);
        }
        else
        {
            parameters.Add(("api_key", Key));
        }

        request.RequestUri = new Uri(
            path + "?" + string.Join('&', parameters.Select(p => $"{p.Name}={Uri.EscapeDataString(p.Value)}")),
            UriKind.Relative);

        using var response = await m_http.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new MediaLookupException("TMDb rejected the configured API key");
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }
}
