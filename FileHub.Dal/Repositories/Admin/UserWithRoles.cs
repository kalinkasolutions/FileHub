using Entities.Account;

namespace Dal.Repositories.Admin;

/// <summary>
/// A user row together with the names of the roles it holds. Exists because
/// <c>UserManager</c> answers "which roles does this user have" one user at a time, which is an
/// N+1 query for a list screen.
/// </summary>
public sealed class UserWithRoles
{
    public required FileHubUser User { get; init; }
    public required string[] Roles { get; init; }
}
