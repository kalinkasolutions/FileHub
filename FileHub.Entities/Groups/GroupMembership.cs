using Entities.Account;

namespace Entities.Groups;

/// <summary>
/// One user's membership of one group. Carries no state of its own, so it cascades from both ends:
/// deleting the group or the account takes it away.
/// </summary>
public sealed class GroupMembership : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }

    public Guid GroupId { get; set; }
    public Group Group { get; set; }

    public Guid UserId { get; set; }
    public FileHubUser User { get; set; }
}
