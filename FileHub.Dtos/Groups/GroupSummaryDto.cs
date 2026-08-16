namespace Dtos.Groups;

/// <summary>
/// A group as an ordinary signed-in user sees it: just enough to pick one as the audience of a
/// share. The counts stay in the admin's <see cref="GroupDto"/> — who else is in a group is not
/// something a member is told here.
/// </summary>
public sealed class GroupSummaryDto
{
    public Guid Id { get; set; }

    public string Name { get; set; }
}
