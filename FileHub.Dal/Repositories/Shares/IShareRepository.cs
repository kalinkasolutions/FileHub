using Entities.Shares;

namespace Dal.Repositories.Shares;

public interface IShareRepository
{
    void Add(Share share);

    /// <summary>One link with its base path loaded — the public routes re-resolve the target from it.</summary>
    Task<Share?> GetAsync(Guid id);

    /// <summary>Every link, with base path and creator, for the admin list.</summary>
    Task<List<Share>> GetAllAsync();

    Task<List<Share>> GetByCreatorAsync(Guid userId);

    void Remove(Share share);

    /// <summary>
    /// Increments the counter in the database rather than through a loaded entity: the public
    /// download route is unauthenticated and can be hit concurrently, and a read-modify-write there
    /// would lose counts — and with them the download limit.
    /// </summary>
    Task IncrementDownloadCountAsync(Guid id);

    Task SaveChangesAsync();
}
