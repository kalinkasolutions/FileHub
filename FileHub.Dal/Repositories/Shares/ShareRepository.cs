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

    public async Task<bool> TryRegisterDownloadAsync(Guid id)
    {
        // One statement, so the check and the increment cannot be interleaved: the database
        // decides, and the affected-row count is the answer. Mirrors Share.DownloadLimitReached —
        // a MaxDownloadCount of 0 or less is unlimited, so the condition holds for every row.
        var updated = await m_context.Shares
            .Where(s => s.Id == id && (s.MaxDownloadCount <= 0 || s.DownloadCount < s.MaxDownloadCount))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DownloadCount, x => x.DownloadCount + 1));

        return updated > 0;
    }

    public Task<int> DeleteForRevokedUsersAsync(Guid basePathId, IReadOnlyCollection<Guid> keptUserIds) =>
        m_context.Shares
            .Where(s => s.BasePathId == basePathId && !keptUserIds.Contains(s.CreatedById))
            .ExecuteDeleteAsync();

    public Task<int> DeleteForRevokedBasePathsAsync(Guid userId, IReadOnlyCollection<Guid> keptBasePathIds) =>
        m_context.Shares
            .Where(s => s.CreatedById == userId && !keptBasePathIds.Contains(s.BasePathId))
            .ExecuteDeleteAsync();

    public Task SaveChangesAsync() => m_context.SaveChangesAsync();
}
