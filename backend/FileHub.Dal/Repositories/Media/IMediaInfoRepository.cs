using Entities.Media;

namespace Dal.Repositories.Media;

/// <summary>Pure data access for the cached look-ups. Who may read one, and when one is stale, is
/// the business layer's call.</summary>
public interface IMediaInfoRepository
{
    Task<MediaInfo?> GetAsync(Guid basePathId, string relativePath);

    Task<MediaInfo?> GetAsync(Guid id);

    Task<List<MediaInfo>> GetForBasePathAsync(Guid basePathId);

    void Add(MediaInfo info);

    void RemoveRange(IEnumerable<MediaInfo> infos);

    Task SaveChangesAsync();
}
