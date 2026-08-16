namespace Dal.Repositories.Admin;

/// <summary>
/// The handful of queries the admin screens need that <c>UserManager</c> cannot answer in one
/// round trip. Everything else an admin does to an account (create, roles, lockout, delete) goes
/// through <c>UserManager</c> itself, which is already the data access for Identity's own tables.
/// </summary>
public interface IUserAdminRepository
{
    /// <summary>Every account with its role names, ordered by display name.</summary>
    Task<List<UserWithRoles>> ListUsersWithRolesAsync();

    /// <summary>How many accounts hold <paramref name="roleName"/>, locked-out ones included.</summary>
    Task<int> CountUsersInRoleAsync(string roleName);

    /// <summary>
    /// The ids of the accounts in <paramref name="roleName"/> that could actually sign in right
    /// now — address confirmed and not locked out. The last-admin guards count these rather than
    /// role holders: an admin who never accepted their invitation, or who is disabled, cannot open
    /// the admin area, so leaving only those behind locks the installation out just as thoroughly
    /// as leaving none behind.
    /// </summary>
    Task<List<Guid>> ListActiveUserIdsInRoleAsync(string roleName);
}
