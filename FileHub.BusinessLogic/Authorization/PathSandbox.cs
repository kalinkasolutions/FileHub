namespace FileHub.BusinessLogic.Authorization;

/// <summary>
/// The only place a caller-supplied path is turned into a path on disk. Everything else — listing,
/// navigating, downloading, sharing — goes through <see cref="TryResolve"/>; nothing builds a path
/// from user input by concatenation.
/// <para>
/// It is the counterpart of the Go build's <c>cleanPath</c>, but stricter in two ways. A climb out
/// of the base path <b>fails</b> instead of being silently clamped back onto it, so a request for
/// the wrong file is answered with "not found" rather than with a different file. And the symlink
/// gap the Go build carried as a known one is closed: every segment below the base path is resolved
/// to its final link target, which has to land inside the base path too.
/// </para>
/// <para>
/// Static functions over already-loaded values: no repository, and no IO beyond the link resolution
/// containment actually needs. Paths are compared ordinally, which is what a case-sensitive
/// filesystem (the deployment target is Linux) does.
/// </para>
/// </summary>
public static class PathSandbox
{
    /// <summary>
    /// Resolves <paramref name="relativePath"/> beneath <paramref name="basePath"/>. Returns false —
    /// and leaves <paramref name="fullPath"/> empty — for anything that would land outside, which is
    /// the only answer a caller should act on. An empty relative path resolves to the base path itself.
    /// </summary>
    public static bool TryResolve(string basePath, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(basePath))
        {
            return false;
        }

        var root = Normalize(basePath);
        var relative = relativePath ?? string.Empty;

        // A rooted or drive-qualified segment replaces the base path rather than extending it:
        // Path.Combine("/srv/media", "/etc") is "/etc". Reject it instead of trimming it, so the
        // wire format has one meaning.
        if (relative.Length > 0 && IsAbsoluteLike(relative))
        {
            return false;
        }

        string candidate;

        try
        {
            // Trimmed so "sub" and "sub/" resolve to one string: everything downstream compares and
            // stores this value, and two spellings of one directory would be two of everything.
            candidate = relative.Length == 0
                ? root
                : Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(root, relative)));
        }
        catch (ArgumentException)
        {
            // Embedded NUL or another character the platform will not accept in a path.
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }

        // GetFullPath has already collapsed every "..", so a climb out shows up here as a path
        // outside the root.
        if (!IsInside(root, candidate))
        {
            return false;
        }

        if (!LinkTargetsStayInside(root, candidate))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }

    /// <summary>
    /// The part of <paramref name="fullPath"/> below <paramref name="basePath"/>, without a leading
    /// separator; empty when it is the base path itself. Meaningful only for a path
    /// <see cref="TryResolve"/> has already accepted — anything else answers empty.
    /// </summary>
    public static string ToRelative(string basePath, string fullPath)
    {
        var root = Normalize(basePath);
        var full = Path.GetFullPath(fullPath);

        return IsInside(root, full) ? Below(root, full) : string.Empty;
    }

    /// <summary>
    /// Resolves every segment below the root, not just the last one: an intermediate link
    /// (<c>base/link/passwd</c> with <c>link -&gt; /etc</c>) escapes just as well as a final one, and
    /// the containment check above cannot see either.
    /// </summary>
    private static bool LinkTargetsStayInside(string root, string candidate)
    {
        var current = root;
        var segments = Below(root, candidate)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);

            if (!SegmentStaysInside(root, current))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SegmentStaysInside(string root, string path)
    {
        var isDirectory = Directory.Exists(path);

        // Nothing on disk here, so there is no link to follow either — a download of a file that was
        // just deleted resolves fine and is reported missing by the caller. (Both Exists checks
        // follow links, so a dangling one lands here too, and is equally unreadable.)
        if (!isDirectory && !File.Exists(path))
        {
            return true;
        }

        try
        {
            // returnFinalTarget walks the whole chain, so one containment check covers a link that
            // points at another link. A path that is not a link at all resolves to null.
            var target = isDirectory
                ? Directory.ResolveLinkTarget(path, returnFinalTarget: true)
                : File.ResolveLinkTarget(path, returnFinalTarget: true);

            return target is null || IsInside(root, Path.GetFullPath(target.FullName));
        }
        catch (IOException)
        {
            // A cyclic link, or one too deep to follow. Nothing readable is behind it.
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsAbsoluteLike(string path)
    {
        if (Path.IsPathRooted(path) || Path.IsPathFullyQualified(path))
        {
            return true;
        }

        // "C:foo" is drive-qualified on Windows and an ordinary file name on Linux; "\\server\share"
        // is a UNC root on Windows and an ordinary file name on Linux. Rejecting both everywhere
        // keeps the wire format platform-independent.
        return (path.Length >= 2 && path[1] == ':') || path.StartsWith(@"\\", StringComparison.Ordinal);
    }

    private static bool IsInside(string root, string candidate) =>
        string.Equals(candidate, root, StringComparison.Ordinal)
        || candidate.StartsWith(Prefix(root), StringComparison.Ordinal);

    private static string Below(string root, string fullPath) =>
        string.Equals(fullPath, root, StringComparison.Ordinal)
            ? string.Empty
            : fullPath[Prefix(root).Length..];

    /// <summary>The root plus a trailing separator — the prefix a contained path must start with.
    /// A filesystem root already ends in one, so appending unconditionally would double it.</summary>
    private static string Prefix(string root) =>
        root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
