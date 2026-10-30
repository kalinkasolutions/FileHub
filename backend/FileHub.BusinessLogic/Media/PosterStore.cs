using Microsoft.Extensions.Options;

namespace FileHub.BusinessLogic.Media;

public sealed class PosterStore : IPosterStore
{
    private readonly string m_directory;

    public PosterStore(IOptions<MediaOptions> options)
    {
        m_directory = Path.GetFullPath(options.Value.PosterPath);
    }

    public string PathOf(Guid mediaInfoId) => Path.Combine(m_directory, $"{mediaInfoId:N}.jpg");

    public bool Exists(Guid mediaInfoId) => File.Exists(PathOf(mediaInfoId));

    public async Task SaveAsync(Guid mediaInfoId, byte[] bytes, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(m_directory);

        // Written beside and renamed over, so a reader never opens a half-written file.
        var final = PathOf(mediaInfoId);
        var partial = final + ".partial";

        await File.WriteAllBytesAsync(partial, bytes, cancellationToken);
        File.Move(partial, final, overwrite: true);
    }

    public void Delete(Guid mediaInfoId)
    {
        File.Delete(PathOf(mediaInfoId));
    }
}
