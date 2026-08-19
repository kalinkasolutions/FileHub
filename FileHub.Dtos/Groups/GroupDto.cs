namespace Dtos.Groups;

/// <summary>One row of the admin's group list.</summary>
public sealed class GroupDto
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    /// <summary>How many accounts belong to this group.</summary>
    public int MemberCount { get; set; }

    /// <summary>How many base paths this group grants its members.</summary>
    public int BasePathCount { get; set; }

    public DateTime CreatedAt { get; set; }
}
