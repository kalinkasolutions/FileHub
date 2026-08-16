using Entities.Shares;
using Microsoft.EntityFrameworkCore;
using Shared;

namespace Dal.Repositories.Shares;

public sealed class ShareRepository : IShareRepository
{
    /// <summary>Identity matches roles on the normalized name, and so does the index behind the join.</summary>
    private static readonly string s_normalizedAdminRole = Roles.Admin.ToUpperInvariant();

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
            .Include(s => s.AudienceGroup)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

    public Task<List<Share>> GetByCreatorAsync(Guid userId) =>
        m_context.Shares
            .Include(s => s.BasePath)
            .Include(s => s.AudienceGroup)
            .Where(s => s.CreatedById == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

    public void Remove(Share share) => m_context.Shares.Remove(share);

    public async Task<bool> TryRegisterDownloadAsync(Guid id, Guid? callerId, bool callerIsAdmin)
    {
        // One statement, so the check and the increment cannot be interleaved: the database
        // decides, and the affected-row count is the answer. Mirrors Share.DownloadLimitReached —
        // a MaxDownloadCount of 0 or less is unlimited, so the condition holds for every row.
        //
        // The audience is re-checked in the same statement rather than trusted from the resolve
        // that came before it: this UPDATE is the one place a redemption is granted, so every rule
        // about who may redeem belongs in its WHERE clause.
        var updated = await m_context.Shares
            .Where(s => s.Id == id)
            .Where(s => s.MaxDownloadCount <= 0 || s.DownloadCount < s.MaxDownloadCount)
            .Where(s => s.AudienceGroupId == null
                        || callerIsAdmin
                        || m_context.GroupMemberships.Any(
                            m => m.GroupId == s.AudienceGroupId && callerId != null && m.UserId == callerId))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DownloadCount, x => x.DownloadCount + 1));

        return updated > 0;
    }

    public Task<int> DeleteSharesLosingBasePathAccessAsync(
        Guid basePathId, IReadOnlyCollection<Guid> pendingUserIds, IReadOnlyCollection<Guid> pendingGroupIds) =>
        m_context.Shares
            .Where(s => s.BasePathId == basePathId)
            .Where(s => !pendingUserIds.Contains(s.CreatedById))
            .Where(s => !m_context.GroupMemberships.Any(
                m => pendingGroupIds.Contains(m.GroupId) && m.UserId == s.CreatedById))
            .Where(s => !AdminUserIds().Contains(s.CreatedById))
            .ExecuteDeleteAsync();

    public Task<int> DeleteSharesOfUserLosingAccessAsync(Guid userId, IReadOnlyCollection<Guid> pendingBasePathIds) =>
        m_context.Shares
            .Where(s => s.CreatedById == userId)
            .Where(s => !pendingBasePathIds.Contains(s.BasePathId))
            .Where(s => !m_context.BasePathGroupAccesses.Any(
                a => a.BasePathId == s.BasePathId && a.Group.Memberships.Any(m => m.UserId == userId)))
            .Where(s => !AdminUserIds().Contains(userId))
            .ExecuteDeleteAsync();

    public Task<int> DeleteSharesLosingGroupAccessAsync(
        Guid groupId, IReadOnlyCollection<Guid> pendingBasePathIds, IReadOnlyCollection<Guid> pendingMemberIds) =>
        m_context.Shares
            // Only links into a base path this group currently holds can be affected by a change to
            // the group: nothing else got its access from here.
            .Where(s => m_context.BasePathGroupAccesses.Any(
                a => a.GroupId == groupId && a.BasePathId == s.BasePathId))
            // Still reachable through this group after the change.
            .Where(s => !(pendingBasePathIds.Contains(s.BasePathId) && pendingMemberIds.Contains(s.CreatedById)))
            // Still reachable by the creator's own grant, by another group, or by the Admin role.
            .Where(s => !s.BasePath.Access.Any(a => a.UserId == s.CreatedById))
            .Where(s => !s.BasePath.GroupAccess.Any(
                a => a.GroupId != groupId && a.Group.Memberships.Any(m => m.UserId == s.CreatedById)))
            .Where(s => !AdminUserIds().Contains(s.CreatedById))
            .ExecuteDeleteAsync();

    public Task SaveChangesAsync() => m_context.SaveChangesAsync();

    /// <summary>
    /// The accounts holding the Admin role, as a subquery. The role is an implicit grant of every
    /// base path, so an admin's links are never the ones a grant change revokes.
    /// </summary>
    private IQueryable<Guid> AdminUserIds() =>
        from userRole in m_context.UserRoles
        join role in m_context.Roles on userRole.RoleId equals role.Id
        where role.NormalizedName == s_normalizedAdminRole
        select userRole.UserId;
}
