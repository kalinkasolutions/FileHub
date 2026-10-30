namespace FileHub.BusinessLogic.Media;

public sealed class HttpPosterFetcher : IPosterFetcher
{
    /// <summary>A w342 TMDb poster is ~30 KB and an IGDB cover_big ~100 KB; anything near this is not a poster.</summary>
    private const long MaxBytes = 5 * 1024 * 1024;

    private readonly HttpClient m_http;

    public HttpPosterFetcher(HttpClient http)
    {
        m_http = http;
    }

    public async Task<byte[]?> FetchAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await m_http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        var length = response.Content.Headers.ContentLength ?? 0;

        if (!mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || length > MaxBytes)
        {
            return null;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        return bytes.Length == 0 || bytes.Length > MaxBytes ? null : bytes;
    }
}
