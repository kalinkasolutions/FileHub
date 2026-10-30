using Entities.Media;
using Microsoft.EntityFrameworkCore;

namespace Dal.Repositories.Media;

public sealed class MediaInfoRepository : IMediaInfoRepository
{
    private readonly FileHubContext m_context;

    public MediaInfoRepository(FileHubContext context)
    {
        m_context = context;
    }

    public Task<MediaInfo?> GetAsync(Guid basePathId, string relativePath) =>
        m_context.MediaInfos.FirstOrDefaultAsync(i => i.BasePathId == basePathId && i.RelativePath == relativePath);

    public Task<MediaInfo?> GetAsync(Guid id) =>
        m_context.MediaInfos.FirstOrDefaultAsync(i => i.Id == id);

    public Task<List<MediaInfo>> GetForBasePathAsync(Guid basePathId) =>
        m_context.MediaInfos.Where(i => i.BasePathId == basePathId).ToListAsync();

    public void Add(MediaInfo info) => m_context.MediaInfos.Add(info);

    public void RemoveRange(IEnumerable<MediaInfo> infos) => m_context.MediaInfos.RemoveRange(infos);

    public Task SaveChangesAsync() => m_context.SaveChangesAsync();
}
