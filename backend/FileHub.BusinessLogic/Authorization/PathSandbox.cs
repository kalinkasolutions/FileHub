namespace FileHub.BusinessLogic.Authorization;

/// <summary>
/// The only place a caller-supplied path is turned into a path on disk. Everything else — listing,
/// navigating, downloading, sharing — goes through <see cref="TryResolve"/>; nothing builds a path
/// from user input by concatenation.
/// <para>
/// It is the counterpart of the Go build's <c>cleanPath</c>, but stricter in two ways. A climb out
/// of the base path <b>fails</b> instead of being silently clamped back onto it, so a request for
/// the wrong file is answered with "not found" rather than with a different file. And the symlink
/// gap the Go build carried as a known one is closed: containment is decided on the fully resolved
/// path, so no link anywhere along the way can point out of the base path.
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
    /// How many links one path may go through before it is refused. A cycle is the reason it exists;
    /// no legitimate path in a served directory comes close.
    /// </summary>
    private const int MaxLinkHops = 40;

    /// <summary>
    /// Resolves <paramref name="relativePath"/> beneath <paramref name="basePath"/>. Returns false —
    /// and leaves <paramref name="fullPath"/> empty — for anything that would land outside, which is
    /// the only answer a caller should act on. An empty relative path resolves to the base path itself.
    /// </summary>
    public static bool TryResolve(string basePath, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;

        var root = ResolveRoot(basePath);

        if (root is null)
        {
            return false;
        }

        return TryResolveUnder(root, relativePath, out fullPath);
    }

    /// <summary>
    /// The base path as the filesystem really sees it, or null when it cannot be resolved at all.
    /// <para>
    /// Resolving a root costs a stat per segment of it, and <see cref="TryResolve"/> pays that on
    /// every call. A caller that resolves many paths under <em>one</em> base path — a directory
    /// listing, a page of share rows — takes the root once with this and hands it to
    /// <see cref="TryResolveUnder"/> and <see cref="ToRelativeUnder"/> instead, which is the same
    /// decision with the root's own resolution lifted out of the loop.
    /// </para>
    /// </summary>
    public static string? ResolveRoot(string basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return null;
        }

        return Normalize(basePath);
    }

    /// <summary>
    /// <see cref="TryResolve"/> for a root <see cref="ResolveRoot"/> has already produced. Every
    /// rule the two-argument version applies is applied here — this *is* that version's body, and
    /// the root is the only thing not re-derived. Passing anything else as
    /// <paramref name="resolvedRoot"/> is what would weaken it, which is why nothing but
    /// <see cref="ResolveRoot"/> produces one.
    /// </summary>
    public static bool TryResolveUnder(string resolvedRoot, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;

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
                ? resolvedRoot
                : Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(resolvedRoot, relative)));
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
        // outside the root. This is the cheap check; it catches the common case without touching
        // the disk, and the real one below catches everything else.
        if (!IsInside(resolvedRoot, candidate))
        {
            return false;
        }

        // The check that actually decides. Anything a link points at — at any depth, through any
        // number of hops — has to still be inside the base path.
        var resolved = RealPath(candidate);

        if (resolved is null || !IsInside(resolvedRoot, resolved))
        {
            return false;
        }

        // The accepted path is the one to open, not its resolved form: opening it is what follows
        // the link, and handing back the target would change what a share stores and what a listing
        // shows. Resolution decides *whether* to answer, not *what* to answer.
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
        var root = ResolveRoot(basePath);

        if (root is null)
        {
            return string.Empty;
        }

        return ToRelativeUnder(root, fullPath);
    }

    /// <summary><see cref="ToRelative"/> for a root <see cref="ResolveRoot"/> has already
    /// produced; see <see cref="TryResolveUnder"/> for why the pair exists.</summary>
    public static string ToRelativeUnder(string resolvedRoot, string fullPath)
    {
        var full = Path.GetFullPath(fullPath);

        return IsInside(resolvedRoot, full) ? Below(resolvedRoot, full) : string.Empty;
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

    /// <summary>
    /// The base path as the filesystem really sees it. The root has to be resolved for the same
    /// reason the candidate does, but in the other direction: with <c>/data -&gt; /mnt/disk1</c>
    /// registered as the base path, everything under it resolves to <c>/mnt/disk1/...</c>, and
    /// against an unresolved root that would read as an escape. Resolving the root is safe in a way
    /// that resolving a caller's path is not — the root is what an admin typed, not what a request
    /// asked for.
    /// </summary>
    private static string? Normalize(string path)
    {
        var full = Path.GetFullPath(path);
        var resolved = RealPath(full);

        return resolved is null ? null : Path.TrimEndingDirectorySeparator(resolved);
    }

    /// <summary>
    /// A path with every symlink resolved — the answer <c>realpath(3)</c> gives, built by hand
    /// because .NET has no equivalent.
    /// <para>
    /// <see cref="FileSystemInfo.ResolveLinkTarget"/> is not one: with <c>returnFinalTarget</c> it
    /// follows a chain of links, but it does not canonicalise the *directories* in the target it
    /// hands back. That is the gap this closes. Given <c>escape -&gt; sub/passwd</c> where
    /// <c>sub -&gt; /etc</c>, it answers <c>&lt;base&gt;/sub/passwd</c> — a string that is lexically
    /// inside the base path, so a containment check on it passes while the open lands in
    /// <c>/etc</c>. Only re-resolving each component of the target catches it.
    /// </para>
    /// <para>
    /// One hop at a time, pushing the target's own segments back onto the work list, so the
    /// components of a target get the same treatment as the components of the original path.
    /// Returns null for a cycle or an unreadable entry, which every caller reads as a refusal.
    /// </para>
    /// </summary>
    private static string? RealPath(string path)
    {
        var pathRoot = Path.GetPathRoot(path);

        if (string.IsNullOrEmpty(pathRoot))
        {
            return null;
        }

        var pending = new Stack<string>();
        PushSegments(pending, path[pathRoot.Length..]);

        var current = pathRoot;
        var hops = 0;

        while (pending.Count > 0)
        {
            var segment = pending.Pop();

            if (string.Equals(segment, ".", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(segment, "..", StringComparison.Ordinal))
            {
                // Everything to the left is already resolved, so climbing lexically here is what
                // the filesystem itself would do.
                current = Path.GetDirectoryName(current) is { Length: > 0 } parent ? parent : pathRoot;
                continue;
            }

            var next = Path.Combine(current, segment);
            string? target;

            try
            {
                target = LinkTarget(next);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }

            if (target is null)
            {
                // Not a link — and a name with nothing on disk behind it lands here too, which is
                // what lets a download of a just-deleted file resolve and be reported missing by
                // the caller rather than as a refusal.
                current = next;
                continue;
            }

            hops = hops + 1;

            if (hops > MaxLinkHops)
            {
                return null;
            }

            var targetRoot = Path.GetPathRoot(target);

            if (string.IsNullOrEmpty(targetRoot))
            {
                // A relative target is relative to the directory holding the link, which is exactly
                // where `current` is standing.
                PushSegments(pending, target);
                continue;
            }

            current = targetRoot;
            PushSegments(pending, target[targetRoot.Length..]);
        }

        return current;
    }

    private static void PushSegments(Stack<string> pending, string path)
    {
        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        for (var i = segments.Length - 1; i >= 0; i--)
        {
            pending.Push(segments[i]);
        }
    }

    /// <summary>
    /// The link's immediate target as written, or null when the entry is not a link (or is not
    /// there at all). One hop on purpose: <see cref="RealPath"/> needs to see each target's own
    /// components rather than the end of a chain.
    /// </summary>
    private static string? LinkTarget(string path)
    {
        FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);

        return info.LinkTarget;
    }
}
