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
    /// Claims one download of a link: a single UPDATE that increments the counter only while the
    /// row is still below its limit, answering whether it did. True means the caller may have the
    /// file. <c>MaxDownloadCount</c> of 0 is unlimited and always succeeds; an unknown id always
    /// fails.
    /// <para>
    /// The condition has to be in the statement. Checking the limit in one round trip and
    /// incrementing in another is a race the public download route loses by design — it is
    /// anonymous, so every concurrent caller reads the same pre-increment count and every one of
    /// them passes.
    /// </para>
    /// </summary>
    Task<bool> TryRegisterDownloadAsync(Guid id);

    /// <summary>
    /// Removes every link into <paramref name="basePathId"/> whose creator is not in
    /// <paramref name="keptUserIds"/>, and answers how many. Revoking a grant has to take the
    /// links with it: the anonymous download path carries no user lookup, so a link created under
    /// a grant that has since been withdrawn keeps serving the file to the internet.
    /// </summary>
    Task<int> DeleteForRevokedUsersAsync(Guid basePathId, IReadOnlyCollection<Guid> keptUserIds);

    /// <summary>
    /// The same revocation from the other end: every link <paramref name="userId"/> created into a
    /// base path that is not in <paramref name="keptBasePathIds"/>.
    /// </summary>
    Task<int> DeleteForRevokedBasePathsAsync(Guid userId, IReadOnlyCollection<Guid> keptBasePathIds);

    Task SaveChangesAsync();
}
