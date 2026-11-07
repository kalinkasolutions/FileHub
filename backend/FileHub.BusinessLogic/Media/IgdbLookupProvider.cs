using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Entities.Paths;
using FileHub.BusinessLogic.Media.Igdb;
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

        var candidates = await QueryAsync(query, cancellationToken);

        return candidates.Count == 0 ? null : Map(Pick(candidates, title));
    }

    /// <summary>The result whose name is the title, else one from the folder's year, else the first:
    /// IGDB ranks by relevance, and relevance puts "Elden Ring: Nightreign" close to "Elden Ring".</summary>
    private static IgdbGame Pick(List<IgdbGame> candidates, ParsedTitle title)
    {
        var wanted = TitleKey.Of(title.Title);

        return candidates.FirstOrDefault(game => TitleKey.Of(game.Name ?? string.Empty) == wanted)
               ?? candidates.FirstOrDefault(game => title.Year is not null && game.Year == title.Year)
               ?? candidates[0];
    }

    private MediaLookup Map(IgdbGame game)
    {
        var platforms = (game.Platforms ?? [])
            .Select(platform => platform.Label ?? string.Empty)
            .Where(label => label.Length > 0)
            .ToList();
        var genres = (game.Genres ?? [])
            .Select(genre => genre.Name ?? string.Empty)
            .Where(name => name.Length > 0)
            .ToList();
        var cover = game.Cover?.ImageId;

        return new MediaLookup(
            MediaType: "game",
            ExternalId: game.Id,
            ExternalUrl: game.Url ?? string.Empty,
            Title: game.Name ?? string.Empty,
            Year: game.Year,
            Overview: game.Summary ?? string.Empty,
            Rating: game.TotalRating is > 0 and var rating ? Math.Round(rating / 10, 1) : null,
            Genres: genres,
            Platforms: platforms,
            PosterUrl: string.IsNullOrEmpty(cover) ? null : $"{m_options.ImageUrl.TrimEnd('/')}/{cover}.jpg");
    }

    private async Task<List<IgdbGame>> QueryAsync(string query, CancellationToken cancellationToken)
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

            return await response.Content.ReadFromJsonAsync<List<IgdbGame>>(SourceJson.Options, cancellationToken) ?? [];
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
