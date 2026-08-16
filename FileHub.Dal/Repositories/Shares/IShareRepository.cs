using Entities.Shares;

namespace Dal.Repositories.Shares;

public interface IShareRepository
{
    void Add(Share share);

    /// <summary>One link with its base path loaded — the public routes re-resolve the target from it.</summary>
    Task<Share?> GetAsync(Guid id);

    /// <summary>Every link, with base path, creator and audience group, for the admin list.</summary>
    Task<List<Share>> GetAllAsync();

    Task<List<Share>> GetByCreatorAsync(Guid userId);

    void Remove(Share share);

    /// <summary>
    /// Claims one download of a link: a single UPDATE that increments the counter only while the
    /// row is still below its limit and the caller is in its audience, answering whether it did.
    /// True means the caller may have the file. <c>MaxDownloadCount</c> of 0 is unlimited and always
    /// succeeds; an unknown id always fails; a null <c>AudienceGroupId</c> is open to everyone,
    /// including a <paramref name="callerId"/> of null.
    /// <para>
    /// The conditions have to be in the statement. Checking the limit in one round trip and
    /// incrementing in another is a race the public download route loses by design — it is
    /// anonymous, so every concurrent caller reads the same pre-increment count and every one of
    /// them passes.
    /// </para>
    /// </summary>
    Task<bool> TryRegisterDownloadAsync(Guid id, Guid? callerId, bool callerIsAdmin);

    /// <summary>
    /// Removes every link into <paramref name="basePathId"/> whose creator will no longer be able
    /// to reach it, and answers how many. The pending state is passed in rather than read back,
    /// because this runs <em>before</em> the grant change is saved: revoking a grant has to take the
    /// links with it, and a failure here must over-revoke rather than leave a live anonymous link
    /// into a base path its creator can no longer browse.
    /// <para>
    /// "Can still reach it" is the whole access model: a direct grant in
    /// <paramref name="pendingUserIds"/>, membership of a group in
    /// <paramref name="pendingGroupIds"/>, or the Admin role.
    /// </para>
    /// </summary>
    Task<int> DeleteSharesLosingBasePathAccessAsync(
        Guid basePathId, IReadOnlyCollection<Guid> pendingUserIds, IReadOnlyCollection<Guid> pendingGroupIds);

    /// <summary>
    /// The same revocation from the user-keyed end: every link <paramref name="userId"/> created
    /// into a base path that is neither in <paramref name="pendingBasePathIds"/> nor reachable
    /// through one of their groups — unless they are an admin, who reaches all of them.
    /// </summary>
    Task<int> DeleteSharesOfUserLosingAccessAsync(Guid userId, IReadOnlyCollection<Guid> pendingBasePathIds);

    /// <summary>
    /// The same revocation for a change to a group — its base paths, its members, or its deletion.
    /// Only links into base paths the group currently holds can be affected; of those, one survives
    /// if the group still grants it to its creator after the change
    /// (<paramref name="pendingBasePathIds"/> × <paramref name="pendingMemberIds"/>), or if the
    /// creator reaches it by their own grant, by another group, or by the Admin role.
    /// <para>Deleting a group passes empty lists for both: nothing survives through it.</para>
    /// </summary>
    Task<int> DeleteSharesLosingGroupAccessAsync(
        Guid groupId, IReadOnlyCollection<Guid> pendingBasePathIds, IReadOnlyCollection<Guid> pendingMemberIds);

    Task SaveChangesAsync();
}
