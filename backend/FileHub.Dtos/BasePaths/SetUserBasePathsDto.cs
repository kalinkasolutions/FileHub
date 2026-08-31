namespace Dtos.BasePaths;

/// <summary>The complete set of base paths one user may see — the same grant table as
/// <see cref="SetBasePathAccessDto"/>, keyed the other way round. An id left out is a revocation.</summary>
public sealed class SetUserBasePathsDto
{
    public List<Guid> BasePathIds { get; set; } = [];
}
