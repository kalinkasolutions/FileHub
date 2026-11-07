using System.Net;
using System.Net.Http.Json;
using FileHub.BusinessLogic.Media.Igdb;
using Microsoft.Extensions.Options;

namespace FileHub.BusinessLogic.Media;

/// <summary>
/// The bearer token IGDB wants, bought from Twitch with the client credentials and kept for as long
/// as Twitch said it lasts. One per process, behind a lock, so a burst of look-ups buys one token
/// rather than one each.
/// </summary>
public sealed class IgdbAccessToken
{
    /// <summary>The named client whose base address is <see cref="IgdbOptions.TokenUrl"/>; see Program.cs.</summary>
    public const string HttpClientName = "twitch";

    private readonly IHttpClientFactory m_httpClientFactory;
    private readonly IgdbOptions m_options;
    private readonly SemaphoreSlim m_lock = new(1, 1);

    private string? m_token;
    private DateTime m_expiresAt;

    public IgdbAccessToken(IHttpClientFactory httpClientFactory, IOptions<IgdbOptions> options)
    {
        m_httpClientFactory = httpClientFactory;
        m_options = options.Value;
    }

    public async Task<string> GetAsync(CancellationToken cancellationToken)
    {
        if (m_token is not null && DateTime.UtcNow < m_expiresAt)
        {
            return m_token;
        }

        await m_lock.WaitAsync(cancellationToken);

        try
        {
            if (m_token is not null && DateTime.UtcNow < m_expiresAt)
            {
                return m_token;
            }

            (m_token, m_expiresAt) = await RequestAsync(cancellationToken);
            return m_token;
        }
        finally
        {
            m_lock.Release();
        }
    }

    /// <summary>Called after IGDB answered 401 to a token that had not expired by our clock.</summary>
    public void Invalidate()
    {
        m_token = null;
    }

    private async Task<(string Token, DateTime ExpiresAt)> RequestAsync(CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = m_options.ClientId.Trim(),
            ["client_secret"] = m_options.ClientSecret.Trim(),
            ["grant_type"] = "client_credentials"
        });

        using var response = await m_httpClientFactory.CreateClient(HttpClientName).PostAsync("oauth2/token", content, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest or HttpStatusCode.Forbidden)
        {
            throw new MediaLookupException("Twitch rejected the configured IGDB client id or secret");
        }

        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TwitchToken>(SourceJson.Options, cancellationToken);

        if (string.IsNullOrEmpty(token?.AccessToken))
        {
            throw new MediaLookupException("Twitch answered without a token");
        }

        // Renewed a little early, so a token never expires between the check and the request.
        var expiresIn = token.ExpiresIn ?? 3600;
        return (token.AccessToken, DateTime.UtcNow.AddSeconds(Math.Max(expiresIn - 300, 60)));
    }
}
