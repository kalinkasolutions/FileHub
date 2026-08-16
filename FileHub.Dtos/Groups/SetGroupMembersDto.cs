namespace Dtos.Groups;

/// <summary>The complete set of members of one group — it replaces the memberships, it does not add
/// to them, so an id left out is a removal.</summary>
public sealed class SetGroupMembersDto
{
    public List<Guid> UserIds { get; set; } = [];
}
