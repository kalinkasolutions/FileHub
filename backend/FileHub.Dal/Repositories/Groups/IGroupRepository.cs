using Entities.Groups;

namespace Dal.Repositories.Groups;

public interface IGroupRepository
{
    /// <summary>Every group with its member and base-path counts, in name order.</summary>
    Task<List<GroupWithCounts>> ListAsync();

    Task<Group?> GetAsync(Guid id);

    /// <summary>
    /// The groups the caller may aim a share at: their own, or every group when
    /// <paramref name="callerIsAdmin"/>.
    /// </summary>
    Task<List<Group>> GetForUserAsync(Guid userId, bool callerIsAdmin);

    /// <summary>
    /// True when another group already holds this name. The comparison follows the column's NOCASE
    /// collation, so it answers exactly what the unique index would have refused — a duplicate is a
    /// clean 400 rather than a unique-index 500.
    /// </summary>
    Task<bool> NameExistsAsync(string name, Guid? excludeId);

    Task<bool> IsMemberAsync(Guid groupId, Guid userId);

    void Add(Group group);

    void Remove(Group group);

    Task<List<Guid>> GetMemberIdsAsync(Guid groupId);

    Task<List<Guid>> GetBasePathIdsAsync(Guid groupId);

    /// <summary>Of the given ids, the ones that are real groups — see <c>FilterExistingUserIdsAsync</c>.</summary>
    Task<List<Guid>> FilterExistingIdsAsync(IReadOnlyCollection<Guid> groupIds);

    /// <summary>Replaces the members of one group; ids left out are removed.</summary>
    Task ReplaceMembersAsync(Guid groupId, IReadOnlyCollection<Guid> userIds);

    /// <summary>Replaces the base paths granted to one group; ids left out are revoked.</summary>
    Task ReplaceBasePathsAsync(Guid groupId, IReadOnlyCollection<Guid> basePathIds);

    Task SaveChangesAsync();
}
