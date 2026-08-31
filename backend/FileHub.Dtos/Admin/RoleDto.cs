namespace Dtos.Admin;

/// <summary>
/// One of the fixed roles, with how many accounts hold it. Roles are seeded at startup and are
/// neither created nor deleted through the API, so this DTO is read-only by design.
/// </summary>
public sealed class RoleDto
{
    public string Name { get; set; }
    public int UserCount { get; set; }
}
