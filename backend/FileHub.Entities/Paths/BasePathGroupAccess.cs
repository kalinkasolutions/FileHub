using Entities.Groups;

namespace Entities.Paths;

/// <summary>
/// Grants one base path to one group, and so to every member of it. The group half of the access
/// model: a user reaches a base path through their own <see cref="BasePathAccess"/> row, through a
/// group they belong to, or by holding the Admin role.
/// </summary>
public sealed class BasePathGroupAccess : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }

    public Guid BasePathId { get; set; }
    public BasePath BasePath { get; set; }

    public Guid GroupId { get; set; }
    public Group Group { get; set; }
}
