using Microsoft.EntityFrameworkCore;

namespace Dal.Repositories.Admin;

public sealed class UserAdminRepository : IUserAdminRepository
{
    private readonly FileHubContext m_context;

    public UserAdminRepository(
        FileHubContext context
    )
    {
        m_context = context;
    }

    public async Task<List<UserWithRoles>> ListUsersWithRolesAsync()
    {
        var users = await m_context.Users
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .ToListAsync();

        // Two round trips rather than one query per user: read every membership once and stitch
        // the names on in memory. The whole table is a handful of rows per account.
        var memberships = await (from userRole in m_context.UserRoles
                join role in m_context.Roles on userRole.RoleId equals role.Id
                select new { userRole.UserId, RoleName = role.Name! })
            .AsNoTracking()
            .ToListAsync();

        var rolesByUser = memberships
            .GroupBy(m => m.UserId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.RoleName).ToArray());

        return users
            .Select(u => new UserWithRoles
            {
                User = u,
                Roles = rolesByUser.TryGetValue(u.Id, out var roleNames) ? roleNames : []
            })
            .ToList();
    }

    public async Task<Dictionary<Guid, int>> CountBasePathGrantsPerUserAsync()
    {
        var counts = await m_context.BasePathAccesses
            .AsNoTracking()
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        return counts.ToDictionary(c => c.UserId, c => c.Count);
    }

    public Task<int> CountUsersInRoleAsync(string roleName)
    {
        // Identity matches on the normalized name, and so does the index behind this join.
        var normalizedRoleName = roleName.ToUpperInvariant();

        return (from userRole in m_context.UserRoles
                join role in m_context.Roles on userRole.RoleId equals role.Id
                where role.NormalizedName == normalizedRoleName
                select userRole.UserId)
            .CountAsync();
    }

    public async Task<List<Guid>> ListActiveUserIdsInRoleAsync(string roleName)
    {
        var normalizedRoleName = roleName.ToUpperInvariant();

        var candidates = await (from user in m_context.Users
                join userRole in m_context.UserRoles on user.Id equals userRole.UserId
                join role in m_context.Roles on userRole.RoleId equals role.Id
                where role.NormalizedName == normalizedRoleName && user.EmailConfirmed
                select new { user.Id, user.LockoutEnd })
            .AsNoTracking()
            .ToListAsync();

        // The lockout window is compared here rather than in the WHERE clause: the SQLite provider
        // cannot translate a DateTimeOffset comparison, and a role holds a handful of accounts.
        var now = DateTimeOffset.UtcNow;

        return candidates
            .Where(c => c.LockoutEnd is null || c.LockoutEnd <= now)
            .Select(c => c.Id)
            .ToList();
    }
}
