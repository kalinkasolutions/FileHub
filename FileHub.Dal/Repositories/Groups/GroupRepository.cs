using Entities.Groups;
using Entities.Paths;
using Microsoft.EntityFrameworkCore;

namespace Dal.Repositories.Groups;

public sealed class GroupRepository : IGroupRepository
{
    private readonly FileHubContext m_context;

    public GroupRepository(FileHubContext context)
    {
        m_context = context;
    }

    public async Task<List<GroupWithCounts>> ListAsync()
    {
        // Both counts projected in the one query: the admin list would otherwise be two round trips
        // per group.
        var rows = await m_context.Groups
            .OrderBy(g => g.Name)
            .Select(g => new
            {
                Group = g,
                MemberCount = g.Memberships.Count,
                BasePathCount = g.BasePathAccess.Count
            })
            .ToListAsync();

        return rows
            .Select(r => new GroupWithCounts
            {
                Group = r.Group,
                MemberCount = r.MemberCount,
                BasePathCount = r.BasePathCount
            })
            .ToList();
    }

    public Task<Group?> GetAsync(Guid id) =>
        m_context.Groups.FirstOrDefaultAsync(g => g.Id == id);

    public Task<List<Group>> GetForUserAsync(Guid userId, bool callerIsAdmin) =>
        m_context.Groups
            .Where(g => callerIsAdmin || g.Memberships.Any(m => m.UserId == userId))
            .OrderBy(g => g.Name)
            .ToListAsync();

    public Task<bool> NameExistsAsync(string name, Guid? excludeId) =>
        m_context.Groups.AnyAsync(g => g.Name == name && (excludeId == null || g.Id != excludeId));

    public Task<bool> IsMemberAsync(Guid groupId, Guid userId) =>
        m_context.GroupMemberships.AnyAsync(m => m.GroupId == groupId && m.UserId == userId);

    public void Add(Group group) => m_context.Groups.Add(group);

    public void Remove(Group group) => m_context.Groups.Remove(group);

    public Task<List<Guid>> GetMemberIdsAsync(Guid groupId) =>
        m_context.GroupMemberships
            .Where(m => m.GroupId == groupId)
            .Select(m => m.UserId)
            .ToListAsync();

    public Task<List<Guid>> GetBasePathIdsAsync(Guid groupId) =>
        m_context.BasePathGroupAccesses
            .Where(a => a.GroupId == groupId)
            .Select(a => a.BasePathId)
            .ToListAsync();

    public async Task<List<Guid>> FilterExistingIdsAsync(IReadOnlyCollection<Guid> groupIds)
    {
        if (groupIds.Count == 0)
        {
            return [];
        }

        return await m_context.Groups
            .AsNoTracking()
            .Where(g => groupIds.Contains(g.Id))
            .Select(g => g.Id)
            .ToListAsync();
    }

    public async Task ReplaceMembersAsync(Guid groupId, IReadOnlyCollection<Guid> userIds)
    {
        // Tracked rather than ExecuteDelete, so the removal and the inserts land in the caller's
        // one SaveChangesAsync — a half-applied membership list is a half-applied permission change.
        var existing = await m_context.GroupMemberships
            .Where(m => m.GroupId == groupId)
            .ToListAsync();

        m_context.GroupMemberships.RemoveRange(existing);

        foreach (var userId in userIds.Distinct())
        {
            m_context.GroupMemberships.Add(new GroupMembership { GroupId = groupId, UserId = userId });
        }
    }

    public async Task ReplaceBasePathsAsync(Guid groupId, IReadOnlyCollection<Guid> basePathIds)
    {
        var existing = await m_context.BasePathGroupAccesses
            .Where(a => a.GroupId == groupId)
            .ToListAsync();

        m_context.BasePathGroupAccesses.RemoveRange(existing);

        foreach (var basePathId in basePathIds.Distinct())
        {
            m_context.BasePathGroupAccesses.Add(
                new BasePathGroupAccess { GroupId = groupId, BasePathId = basePathId });
        }
    }

    public Task SaveChangesAsync() => m_context.SaveChangesAsync();
}
