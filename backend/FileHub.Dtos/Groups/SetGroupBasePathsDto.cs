namespace Dtos.Groups;

/// <summary>The complete set of base paths one group grants — an id left out is a revocation, and a
/// revocation takes the links its members made under it.</summary>
public sealed class SetGroupBasePathsDto
{
    public List<Guid> BasePathIds { get; set; } = [];
}
