using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Entities.Paths;
using FileHub.BusinessLogic.Media.Tmdb;
using Microsoft.Extensions.Options;

namespace FileHub.BusinessLogic.Media;

/// <summary>
/// Movies and series from The Movie Database. Two calls per look-up: a multi search that says
/// whether the name is a movie or a show, then that title's own page for the genres and the
/// overview. An id tag in the folder name skips the search.
/// </summary>
public sealed class TmdbLookupProvider : IMediaLookupProvider
{
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
        var response = await GetAsync<TmdbSearchResponse>(
            "search/multi", [("query", query), ("include_adult", "false")], cancellationToken);

        (string Type, long Id)? first = null;

        foreach (var result in response?.Results ?? [])
        {
            if (result.MediaType is not ("movie" or "tv"))
            {
                continue;
            }

            var candidate = (result.MediaType, result.Id);
            first ??= candidate;

            // The year is the one thing the folder name says that a search cannot filter on here, so
            // the first result from that year beats the first result overall.
            if (year is null || result.Year == year)
            {
                return candidate;
            }
        }

        return first;
    }

    private async Task<MediaLookup?> DetailsAsync(string type, long id, CancellationToken cancellationToken)
    {
        var title = await GetAsync<TmdbTitle>($"{type}/{id}", [], cancellationToken);

        if (title is null)
        {
            return null;
        }

        var genres = (title.Genres ?? [])
            .Select(genre => genre.Name ?? string.Empty)
            .Where(name => name.Length > 0)
            .ToList();

        return new MediaLookup(
            MediaType: type,
            ExternalId: id,
            ExternalUrl: $"{m_options.SiteUrl.TrimEnd('/')}/{type}/{id}",
            Title: title.DisplayTitle,
            Year: title.Year,
            Overview: title.Overview ?? string.Empty,
            Rating: title.VoteAverage is > 0 and var rating ? Math.Round(rating, 1) : null,
            Genres: genres,
            Platforms: [],
            PosterUrl: string.IsNullOrEmpty(title.PosterPath) ? null : m_options.ImageUrl.TrimEnd('/') + title.PosterPath);
    }

    /// <summary>One GET against the v3 API; null for a 404, an exception for anything else wrong.</summary>
    private async Task<T?> GetAsync<T>(
        string path, IEnumerable<(string Name, string Value)> query, CancellationToken cancellationToken) where T : class
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

        return await response.Content.ReadFromJsonAsync<T>(SourceJson.Options, cancellationToken);
    }
}
