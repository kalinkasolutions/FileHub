namespace FileHub.BusinessLogic.Services.Files;

/// <summary>
/// A download target that has been through the sandbox. It carries an absolute path on the host, so
/// it deliberately lives here and not in <c>FileHub.Dtos</c> — nothing in this type is ever
/// serialized to a client; the endpoint uses it to open the file and throws it away.
/// </summary>
public sealed class ResolvedFile
{
    public required string FullPath { get; init; }

    /// <summary>What the download should be called, which is not necessarily the last path segment:
    /// a base path uses its label.</summary>
    public required string Name { get; init; }

    public required bool IsDirectory { get; init; }
}
