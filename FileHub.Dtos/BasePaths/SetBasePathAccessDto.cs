namespace Dtos.BasePaths;

/// <summary>The complete set of users allowed to see one base path — it replaces the grants, it does
/// not add to them, so an id left out is a revocation.</summary>
public sealed class SetBasePathAccessDto
{
    public List<Guid> UserIds { get; set; } = [];
}
