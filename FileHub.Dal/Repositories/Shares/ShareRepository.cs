using Entities.Shares;
using Microsoft.EntityFrameworkCore;

namespace Dal.Repositories.Shares;

public sealed class ShareRepository : IShareRepository
{
    private readonly FileHubContext m_context;

    public ShareRepository(FileHubContext context)
    {
        m_context = context;
    }

    public void Add(Share share) => m_context.Shares.Add(share);

    public Task<Share?> GetAsync(Guid id) =>
        m_context.Shares
            .Include(s => s.BasePath)
            .FirstOrDefaultAsync(s => s.Id == id);

    public Task<List<Share>> GetAllAsync() =>
        m_context.Shares
            .Include(s => s.BasePath)
            .Include(s => s.CreatedBy)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

    public Task<List<Share>> GetByCreatorAsync(Guid userId) =>
        m_context.Shares
            .Include(s => s.BasePath)
            .Where(s => s.CreatedById == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

    public void Remove(Share share) => m_context.Shares.Remove(share);

    public Task IncrementDownloadCountAsync(Guid id) =>
        m_context.Shares
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DownloadCount, x => x.DownloadCount + 1));

    public Task UpdateSizeAsync(Guid id, long size) =>
        m_context.Shares
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Size, size));

    public Task SaveChangesAsync() => m_context.SaveChangesAsync();
}
