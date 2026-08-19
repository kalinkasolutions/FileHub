namespace Dtos.BasePaths;

/// <summary>
/// A configured base path. Admin-only: the absolute path on the host is on it, and only the admin
/// area ever sees one — a browsing user gets a <c>FileEntryDto</c> instead.
/// </summary>
public sealed class BasePathDto
{
    public Guid Id { get; set; }

    public string Path { get; set; }

    /// <summary>Label shown in the UI; the directory name when the admin left it empty.</summary>
    public string Name { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>How many users have been granted this base path directly.</summary>
    public int UserCount { get; set; }

    /// <summary>How many groups have been granted this base path. Their members reach it too.</summary>
    public int GroupCount { get; set; }
}
