namespace Dtos.BasePaths;

/// <summary>The complete set of groups allowed to see one base path — the group-side mirror of
/// <see cref="SetBasePathAccessDto"/>. An id left out is a revocation.</summary>
public sealed class SetBasePathGroupsDto
{
    public List<Guid> GroupIds { get; set; } = [];
}
