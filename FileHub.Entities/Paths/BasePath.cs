using System.Collections.Generic;
using Entities.Shares;

namespace Entities.Paths;

/// <summary>
/// A directory on the host that FileHub is allowed to read. Nothing outside a base path is
/// reachable: every caller-supplied path is resolved beneath one of these.
/// </summary>
public sealed class BasePath : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>Absolute path on the host, stored cleaned (no trailing separator, no "..").</summary>
    public string Path { get; set; }

    /// <summary>Label shown in the UI; falls back to the directory name when empty.</summary>
    public string Name { get; set; }

    public ICollection<BasePathAccess> Access { get; set; } = new List<BasePathAccess>();
    public ICollection<Share> Shares { get; set; } = new List<Share>();
}
