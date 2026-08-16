using Entities.Account;

namespace Entities.Paths;

/// <summary>
/// Grants one user access to one base path. A user with no row for a base path cannot see it,
/// navigate into it, download from it or share it — admins included, until they grant it to
/// themselves. There is no "all paths" wildcard: absence of a row is always a denial.
/// </summary>
public sealed class BasePathAccess : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }

    public Guid BasePathId { get; set; }
    public BasePath BasePath { get; set; }

    public Guid UserId { get; set; }
    public FileHubUser User { get; set; }
}
