namespace FileHub.BusinessLogic.Media;

/// <summary>The poster files, one per cached look-up and named by its id. The store knows the
/// directory; nothing else does.</summary>
public interface IPosterStore
{
    /// <summary>Absolute, because <c>Results.File</c> resolves a relative path against the web root.</summary>
    string PathOf(Guid mediaInfoId);

    bool Exists(Guid mediaInfoId);

    Task SaveAsync(Guid mediaInfoId, byte[] bytes, CancellationToken cancellationToken);

    void Delete(Guid mediaInfoId);
}
