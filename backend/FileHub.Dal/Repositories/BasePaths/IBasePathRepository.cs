using Entities.Paths;

namespace Dal.Repositories.BasePaths;

public interface IBasePathRepository
{
    /// <summary>Every base path, in name order, each with the number of users and groups granted it.</summary>
    Task<List<BasePathWithCounts>> GetAllAsync();

    Task<BasePath?> GetAsync(Guid id);

    /// <summary>
    /// Every base path the user can reach, in name order: their own grants, the grants of every
    /// group they belong to, and — when <paramref name="callerIsAdmin"/> — all of them.
    /// </summary>
    Task<List<BasePath>> GetForUserAsync(Guid userId, bool callerIsAdmin);

    /// <summary>
    /// One base path, but only if the user can reach it — null otherwise, which callers answer as
    /// "not found". This is the query every browsing, download and share request starts from.
    /// <para>
    /// <paramref name="callerIsAdmin"/> is an argument rather than something resolved from an
    /// injected accessor down here, so a reader of the query can see what decides it: the Admin
    /// role is an implicit grant of every base path, and the endpoint is where that is read off the
    /// principal.
    /// </para>
    /// </summary>
    Task<BasePath?> GetForUserAsync(Guid id, Guid userId, bool callerIsAdmin);

    /// <summary>True when another row already points at this directory (the unique index would reject it).</summary>
    Task<bool> PathExistsAsync(string path, Guid? excludeId);

    void Add(BasePath basePath);

    void Remove(BasePath basePath);

    Task<List<Guid>> GetUserIdsAsync(Guid basePathId);

    /// <summary>The groups granted this base path.</summary>
    Task<List<Guid>> GetGroupIdsAsync(Guid basePathId);

    /// <summary>
    /// Of the given ids, the ones that are real accounts. The grant table's foreign key would
    /// reject the others at save time, taking the whole grant change down with them.
    /// </summary>
    Task<List<Guid>> FilterExistingUserIdsAsync(IReadOnlyCollection<Guid> userIds);

    /// <summary>
    /// Of the given ids, the ones that are real base paths — the counterpart of
    /// <see cref="FilterExistingUserIdsAsync"/> for the other end of the same grant tables.
    /// </summary>
    Task<List<Guid>> FilterExistingIdsAsync(IReadOnlyCollection<Guid> basePathIds);

    Task<List<Guid>> GetBasePathIdsAsync(Guid userId);

    /// <summary>Replaces the user grants for one base path; ids left out are revoked.</summary>
    Task ReplaceAccessForBasePathAsync(Guid basePathId, IReadOnlyCollection<Guid> userIds);

    /// <summary>Replaces the grants for one user; ids left out are revoked.</summary>
    Task ReplaceAccessForUserAsync(Guid userId, IReadOnlyCollection<Guid> basePathIds);

    /// <summary>Replaces the group grants for one base path; ids left out are revoked.</summary>
    Task ReplaceGroupAccessForBasePathAsync(Guid basePathId, IReadOnlyCollection<Guid> groupIds);

    Task SaveChangesAsync();
}
