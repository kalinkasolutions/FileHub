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

    public async Task<List<BasePathWithCounts>> GetAllAsync()
    {
        // Both counts projected in the one query, the way the group list does it. Including the two
        // grant collections instead joined them against each other — twelve users and three groups
        // is thirty-six rows for one base path — and EF says so as MultipleCollectionIncludeWarning.
        var rows = await m_context.BasePaths
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Path)
            .Select(p => new
            {
                BasePath = p,
                UserCount = p.Access.Count,
                GroupCount = p.GroupAccess.Count
            })
            .ToListAsync();

        return rows
            .Select(r => new BasePathWithCounts
            {
                BasePath = r.BasePath,
                UserCount = r.UserCount,
                GroupCount = r.GroupCount
            })
            .ToList();
    }

    public Task<BasePath?> GetAsync(Guid id) =>
        m_context.BasePaths.FirstOrDefaultAsync(p => p.Id == id);

    // The three routes to a base path, in one query rather than one per route: the caller's own
    // grant, a grant to any group they belong to, or the Admin role — which the endpoint decides
    // and passes in, so the query says out loud what makes it true.
    public Task<List<BasePath>> GetForUserAsync(Guid userId, bool callerIsAdmin) =>
        m_context.BasePaths
            .Where(p => callerIsAdmin
                        || p.Access.Any(a => a.UserId == userId)
                        || p.GroupAccess.Any(g => g.Group.Memberships.Any(m => m.UserId == userId)))
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Path)
            .ToListAsync();

    public Task<BasePath?> GetForUserAsync(Guid id, Guid userId, bool callerIsAdmin) =>
        m_context.BasePaths
            .FirstOrDefaultAsync(p => p.Id == id
                                      && (callerIsAdmin
                                          || p.Access.Any(a => a.UserId == userId)
                                          || p.GroupAccess.Any(g => g.Group.Memberships.Any(m => m.UserId == userId))));

    public Task<bool> PathExistsAsync(string path, Guid? excludeId) =>
        m_context.BasePaths.AnyAsync(p => p.Path == path && (excludeId == null || p.Id != excludeId));

    public void Add(BasePath basePath) => m_context.BasePaths.Add(basePath);

    public void Remove(BasePath basePath) => m_context.BasePaths.Remove(basePath);

    public Task<List<Guid>> GetUserIdsAsync(Guid basePathId) =>
        m_context.BasePathAccesses
            .Where(a => a.BasePathId == basePathId)
            .Select(a => a.UserId)
            .ToListAsync();

    public Task<List<Guid>> GetGroupIdsAsync(Guid basePathId) =>
        m_context.BasePathGroupAccesses
            .Where(a => a.BasePathId == basePathId)
            .Select(a => a.GroupId)
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

    public async Task<List<Guid>> FilterExistingIdsAsync(IReadOnlyCollection<Guid> basePathIds)
    {
        if (basePathIds.Count == 0)
        {
            return [];
        }

        return await m_context.BasePaths
            .AsNoTracking()
            .Where(p => basePathIds.Contains(p.Id))
            .Select(p => p.Id)
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

    public async Task ReplaceGroupAccessForBasePathAsync(Guid basePathId, IReadOnlyCollection<Guid> groupIds)
    {
        var existing = await m_context.BasePathGroupAccesses
            .Where(a => a.BasePathId == basePathId)
            .ToListAsync();

        m_context.BasePathGroupAccesses.RemoveRange(existing);

        foreach (var groupId in groupIds.Distinct())
        {
            m_context.BasePathGroupAccesses.Add(
                new BasePathGroupAccess { BasePathId = basePathId, GroupId = groupId });
        }
    }

    public Task SaveChangesAsync() => m_context.SaveChangesAsync();
}
