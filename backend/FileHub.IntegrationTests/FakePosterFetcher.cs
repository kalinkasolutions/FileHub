using FileHub.BusinessLogic.Media;

namespace FileHub.IntegrationTests;

/// <summary>Hands back a few bytes for any URL, or nothing when told the image is unreachable.</summary>
public sealed class FakePosterFetcher : IPosterFetcher
{
    public bool Available { get; set; } = true;
    public List<string> Fetched { get; } = [];

    public Task<byte[]?> FetchAsync(string url, CancellationToken cancellationToken)
    {
        Fetched.Add(url);
        return Task.FromResult(Available ? new byte[] { 0xFF, 0xD8, 0xFF } : null);
    }
}
