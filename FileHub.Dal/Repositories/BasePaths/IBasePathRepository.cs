using Entities.Paths;

namespace Dal.Repositories.BasePaths;

public interface IBasePathRepository
{
    Task<List<BasePath>> GetAllAsync();

    Task<BasePath?> GetAsync(Guid id);

    /// <summary>Every base path the user has been granted, in name order.</summary>
    Task<List<BasePath>> GetForUserAsync(Guid userId);

    /// <summary>
    /// One base path, but only if the user holds a grant for it — null otherwise, which callers
    /// answer as "not found". This is the query every browsing, download and share request starts
    /// from; absence of a grant row is a denial, admins included.
    /// </summary>
    Task<BasePath?> GetForUserAsync(Guid id, Guid userId);

    /// <summary>True when another row already points at this directory (the unique index would reject it).</summary>
    Task<bool> PathExistsAsync(string path, Guid? excludeId);

    void Add(BasePath basePath);

    void Remove(BasePath basePath);

    Task<List<Guid>> GetUserIdsAsync(Guid basePathId);

    Task<List<Guid>> GetBasePathIdsAsync(Guid userId);

    /// <summary>Replaces the grants for one base path; ids left out are revoked.</summary>
    Task ReplaceAccessForBasePathAsync(Guid basePathId, IReadOnlyCollection<Guid> userIds);

    /// <summary>Replaces the grants for one user; ids left out are revoked.</summary>
    Task ReplaceAccessForUserAsync(Guid userId, IReadOnlyCollection<Guid> basePathIds);

    Task SaveChangesAsync();
}
