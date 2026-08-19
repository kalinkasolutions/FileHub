using Entities.Account;

namespace Entities.Paths;

/// <summary>
/// Grants one user access to one base path, directly. It is one of the three routes to a base
/// path: this row, a <see cref="BasePathGroupAccess"/> row for a group the user belongs to, or the
/// Admin role — which is an implicit grant of every base path. For everyone else, absence of a row
/// on either side is a denial.
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
