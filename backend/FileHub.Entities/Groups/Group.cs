using Entities.Paths;
using Entities.Shares;

namespace Entities.Groups;

/// <summary>
/// A named set of users. A group exists to be granted base paths and to be aimed at by a share:
/// a user's effective access is the union of their own grants and the grants of every group they
/// belong to.
/// </summary>
public sealed class Group : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>Unique, stored trimmed. The comparison is case-insensitive (NOCASE), so "Family"
    /// and "family" are the same group rather than two that look alike in the admin list.</summary>
    public string Name { get; set; }

    public ICollection<GroupMembership> Memberships { get; set; } = new List<GroupMembership>();

    public ICollection<BasePathGroupAccess> BasePathAccess { get; set; } = new List<BasePathGroupAccess>();

    /// <summary>The links aimed at this group. They cascade with it — see <c>Share.AudienceGroupId</c>.</summary>
    public ICollection<Share> Shares { get; set; } = new List<Share>();
}
