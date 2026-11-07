using FileHub.BusinessLogic.Media;
using Microsoft.Extensions.Options;

namespace FileHub.IntegrationTests;

/// <summary>The real poster store over a temporary directory that does not exist until the first
/// save — which is the state of a fresh install.</summary>
public sealed class PosterStoreTests : IDisposable
{
    private readonly string m_directory = Path.Combine(Path.GetTempPath(), "filehub-posters-" + Guid.NewGuid().ToString("N"));

    private PosterStore Store => new(Options.Create(new MediaOptions { PosterPath = m_directory }));

    [Fact]
    public void Deleting_a_poster_that_was_never_saved_is_not_an_error()
    {
        Store.Delete(Guid.NewGuid());

        Assert.False(Directory.Exists(m_directory));
    }

    [Fact]
    public async Task A_saved_poster_exists_until_it_is_deleted()
    {
        var id = Guid.NewGuid();

        await Store.SaveAsync(id, [0xFF, 0xD8, 0xFF], CancellationToken.None);
        Assert.True(Store.Exists(id));
        Assert.Equal(Path.Combine(m_directory, $"{id:N}.jpg"), Store.PathOf(id));

        Store.Delete(id);
        Assert.False(Store.Exists(id));
    }

    public void Dispose()
    {
        if (Directory.Exists(m_directory))
        {
            Directory.Delete(m_directory, recursive: true);
        }
    }
}
