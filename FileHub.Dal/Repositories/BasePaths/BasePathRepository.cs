using Entities.Paths;
using Microsoft.EntityFrameworkCore;

namespace Dal.Repositories.BasePaths;

public sealed class BasePathRepository : IBasePathRepository
{
    private readonly FileHubContext m_context;

    public BasePathRepository(FileHubContext context)
    {
        m_context = context;
    }

    public Task<List<BasePath>> GetAllAsync() =>
        m_context.BasePaths
            .Include(p => p.Access)
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Path)
            .ToListAsync();

    public Task<BasePath?> GetAsync(Guid id) =>
        m_context.BasePaths.FirstOrDefaultAsync(p => p.Id == id);

    public Task<List<BasePath>> GetForUserAsync(Guid userId) =>
        m_context.BasePaths
            .Where(p => p.Access.Any(a => a.UserId == userId))
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Path)
            .ToListAsync();

    public Task<BasePath?> GetForUserAsync(Guid id, Guid userId) =>
        m_context.BasePaths
            .FirstOrDefaultAsync(p => p.Id == id && p.Access.Any(a => a.UserId == userId));

    public Task<bool> PathExistsAsync(string path, Guid? excludeId) =>
        m_context.BasePaths.AnyAsync(p => p.Path == path && (excludeId == null || p.Id != excludeId));

    public void Add(BasePath basePath) => m_context.BasePaths.Add(basePath);

    public void Remove(BasePath basePath) => m_context.BasePaths.Remove(basePath);

    public Task<List<Guid>> GetUserIdsAsync(Guid basePathId) =>
        m_context.BasePathAccesses
            .Where(a => a.BasePathId == basePathId)
            .Select(a => a.UserId)
            .ToListAsync();

    public async Task<List<Guid>> FilterExistingUserIdsAsync(IReadOnlyCollection<Guid> userIds)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        return await m_context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync();
    }

    public Task<List<Guid>> GetBasePathIdsAsync(Guid userId) =>
        m_context.BasePathAccesses
            .Where(a => a.UserId == userId)
            .Select(a => a.BasePathId)
            .ToListAsync();

    public async Task ReplaceAccessForBasePathAsync(Guid basePathId, IReadOnlyCollection<Guid> userIds)
    {
        // Tracked rather than ExecuteDelete, so the removal and the inserts land in the caller's
        // one SaveChangesAsync — a half-applied grant list is a half-applied permission change.
        var existing = await m_context.BasePathAccesses
            .Where(a => a.BasePathId == basePathId)
            .ToListAsync();

        m_context.BasePathAccesses.RemoveRange(existing);

        foreach (var userId in userIds.Distinct())
        {
            m_context.BasePathAccesses.Add(new BasePathAccess { BasePathId = basePathId, UserId = userId });
        }
    }

    public async Task ReplaceAccessForUserAsync(Guid userId, IReadOnlyCollection<Guid> basePathIds)
    {
        var existing = await m_context.BasePathAccesses
            .Where(a => a.UserId == userId)
            .ToListAsync();

        m_context.BasePathAccesses.RemoveRange(existing);

        foreach (var basePathId in basePathIds.Distinct())
        {
            m_context.BasePathAccesses.Add(new BasePathAccess { BasePathId = basePathId, UserId = userId });
        }
    }

    public Task SaveChangesAsync() => m_context.SaveChangesAsync();
}
