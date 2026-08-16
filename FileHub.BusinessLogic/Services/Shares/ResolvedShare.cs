namespace FileHub.BusinessLogic.Services.Shares;

/// <summary>
/// A public link whose target has been re-resolved through the sandbox. It carries an absolute path
/// on the host, so — like <c>ResolvedFile</c> — it lives here rather than in <c>FileHub.Dtos</c>:
/// the endpoint streams from it and projects the handful of fields the public page may see.
/// </summary>
public sealed class ResolvedShare
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public required bool IsDirectory { get; init; }

    /// <summary>The size stored on the row, measured once when the link was created.</summary>
    public required long Size { get; init; }
}
