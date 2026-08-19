using Entities.Groups;

namespace Dal.Repositories.Groups;

/// <summary>
/// A group row together with the two numbers the admin list shows. Exists so the list is one query
/// with two projected counts rather than a membership and a grant query per group.
/// </summary>
public sealed class GroupWithCounts
{
    public required Group Group { get; init; }
    public required int MemberCount { get; init; }
    public required int BasePathCount { get; init; }
}
