using FileHub.BusinessLogic.Media;

namespace FileHub.IntegrationTests;

/// <summary>The poster files, in memory: what matters is which ids have one, not the bytes on disk.</summary>
public sealed class FakePosterStore : IPosterStore
{
    public Dictionary<Guid, byte[]> Files { get; } = [];

    public string PathOf(Guid mediaInfoId) => $"/posters/{mediaInfoId:N}.jpg";

    public bool Exists(Guid mediaInfoId) => Files.ContainsKey(mediaInfoId);

    public Task SaveAsync(Guid mediaInfoId, byte[] bytes, CancellationToken cancellationToken)
    {
        Files[mediaInfoId] = bytes;
        return Task.CompletedTask;
    }

    public void Delete(Guid mediaInfoId) => Files.Remove(mediaInfoId);
}
