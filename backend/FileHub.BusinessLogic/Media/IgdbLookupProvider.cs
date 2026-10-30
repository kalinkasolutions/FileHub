using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Entities.Paths;
using Microsoft.Extensions.Options;

namespace FileHub.BusinessLogic.Media;

/// <summary>
/// Games from IGDB. One query per look-up, in IGDB's own query language: a search for the title
/// with the fields the popover shows, excluding alternate editions so "Cyberpunk 2077" is the game
/// and not its Ultimate Edition. An id tag in the folder name asks for that game outright.
/// </summary>
public sealed class IgdbLookupProvider : IMediaLookupProvider
{
    private const string Fields =
        "name,url,summary,first_release_date,total_rating,cover.image_id,genres.name,platforms.abbreviation,platforms.name";

    private readonly HttpClient m_http;
    private readonly IgdbAccessToken m_token;
    private readonly IgdbOptions m_options;

    public IgdbLookupProvider(HttpClient http, IgdbAccessToken token, IOptions<IgdbOptions> options)
    {
        m_http = http;
        m_token = token;
        m_options = options.Value;
    }

    public BasePathKind Kind => BasePathKind.Games;
    public string Name => "IGDB";
    public string Source => "igdb";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(m_options.ClientId) && !string.IsNullOrWhiteSpace(m_options.ClientSecret);

    public string NotConfiguredHint => "set Igdb__ClientId and Igdb__ClientSecret";

    public async Task<MediaLookup?> LookupAsync(ParsedTitle title, CancellationToken cancellationToken)
    {
        var query = title.IgdbId is { } id
            ? $"fields {Fields}; where id = {id};"
            : $"search \"{Escape(title.Title)}\"; fields {Fields}; where version_parent = null; limit 10;";

        using var document = await QueryAsync(query, cancellationToken);
        var candidates = document.RootElement.EnumerateArray().ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        return Map(Pick(candidates, title));
    }

    /// <summary>The result whose name is the title, else one from the folder's year, else the first:
    /// IGDB ranks by relevance, and relevance puts "Elden Ring: Nightreign" close to "Elden Ring".</summary>
    private static JsonElement Pick(List<JsonElement> candidates, ParsedTitle title)
    {
        var wanted = TitleKey.Of(title.Title);
        var exact = candidates.FirstOrDefault(c => TitleKey.Of(c.GetProperty("name").GetString() ?? string.Empty) == wanted);

        if (exact.ValueKind == JsonValueKind.Object)
        {
            return exact;
        }

        if (title.Year is { } year)
        {
            var sameYear = candidates.FirstOrDefault(c => YearOf(c) == year);
            if (sameYear.ValueKind == JsonValueKind.Object)
            {
                return sameYear;
            }
        }

        return candidates[0];
    }

    private static MediaLookup Map(JsonElement game)
    {
        var rating = game.TryGetProperty("total_rating", out var total) ? total.GetDouble() : 0;
        var cover = game.TryGetProperty("cover", out var coverElement) && coverElement.TryGetProperty("image_id", out var imageId)
            ? imageId.GetString()
            : null;

        return new MediaLookup(
            MediaType: "game",
            ExternalId: game.GetProperty("id").GetInt64(),
            ExternalUrl: game.TryGetProperty("url", out var url) ? url.GetString() ?? string.Empty : string.Empty,
            Title: game.GetProperty("name").GetString() ?? string.Empty,
            Year: YearOf(game),
            Overview: game.TryGetProperty("summary", out var summary) ? summary.GetString() ?? string.Empty : string.Empty,
            Rating: rating > 0 ? Math.Round(rating / 10, 1) : null,
            Genres: Names(game, "genres", "name"),
            Platforms: Names(game, "platforms", "abbreviation", "name"),
            PosterUrl: string.IsNullOrEmpty(cover) ? null : $"https://images.igdb.com/igdb/image/upload/t_cover_big/{cover}.jpg");
    }

    private static List<string> Names(JsonElement game, string collection, params string[] fields)
    {
        if (!game.TryGetProperty(collection, out var items))
        {
            return [];
        }

        var names = new List<string>();

        foreach (var item in items.EnumerateArray())
        {
            foreach (var field in fields)
            {
                if (item.TryGetProperty(field, out var value) && value.GetString() is { Length: > 0 } name)
                {
                    names.Add(name);
                    break;
                }
            }
        }

        return names;
    }

    private static int? YearOf(JsonElement game)
    {
        if (!game.TryGetProperty("first_release_date", out var date) || date.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(date.GetInt64()).Year;
    }

    private async Task<JsonDocument> QueryAsync(string query, CancellationToken cancellationToken)
    {
        var response = await SendAsync(query, cancellationToken);

        // Twitch revokes a token when the app's secret is rotated; one fresh token settles it.
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            m_token.Invalidate();
            response = await SendAsync(query, cancellationToken);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new MediaLookupException("IGDB rejected the configured client id");
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(string query, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "games");
        request.Headers.Add("Client-ID", m_options.ClientId.Trim());
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await m_token.GetAsync(cancellationToken));
        request.Content = new StringContent(query, Encoding.UTF8, "text/plain");

        return await m_http.SendAsync(request, cancellationToken);
    }

    private static string Escape(string title) => title.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
