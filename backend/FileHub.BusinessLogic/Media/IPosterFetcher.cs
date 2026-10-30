namespace FileHub.BusinessLogic.Media;

/// <summary>Downloads one poster. Null for anything that is not an image of a sane size — a
/// missing poster is a blank, not a failed look-up.</summary>
public interface IPosterFetcher
{
    Task<byte[]?> FetchAsync(string url, CancellationToken cancellationToken);
}
