namespace FileHub.IntegrationTests;

/// <summary>
/// A throwaway directory tree under the system temp directory. The filesystem half of FileHub —
/// the sandbox, the listing shapes, the size a share is measured with — reads the real disk, and a
/// faked one would only test the fake. Each fixture owns one and deletes it on dispose.
/// </summary>
public sealed class TempTree : IDisposable
{
    /// <summary>
    /// Whether this platform will create a symbolic link at all. Probed once, because the link
    /// rules are the most valuable thing <c>PathSandbox</c> does and a test that cannot exercise
    /// them has to say so rather than report a meaningless pass.
    /// </summary>
    public static bool SymlinksSupported { get; } = ProbeSymlinks();

    /// <summary>Absolute path of this tree's root, with every symlink in it already resolved.</summary>
    public string Root { get; }

    /// <summary>
    /// A second root outside <see cref="Root"/>, so a test has somewhere to point an escaping
    /// symlink at that is still cleaned up afterwards.
    /// </summary>
    public string Outside { get; }

    private readonly string m_container;

    public TempTree()
    {
        // The platform temp directory is itself a symlink on some systems (/tmp -> /private/tmp on
        // macOS). PathSandbox compares *resolved* link targets against the root, so a root that
        // still contains a link would make every "points back inside" case look like an escape.
        var temp = Path.TrimEndingDirectorySeparator(Path.GetTempPath());
        var resolvedTemp = Directory.ResolveLinkTarget(temp, returnFinalTarget: true)?.FullName ?? temp;

        m_container = Path.Combine(resolvedTemp, "filehub-tests", Guid.NewGuid().ToString("N"));
        Root = Path.Combine(m_container, "base");
        Outside = Path.Combine(m_container, "outside");

        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Outside);
    }

    /// <summary>Creates a directory below the root and returns its absolute path.</summary>
    public string Dir(string relativePath)
    {
        var full = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(full);
        return full;
    }

    /// <summary>Creates a file below the root, with its parent directories, and returns its absolute path.</summary>
    public string File(string relativePath, string content = "")
    {
        var full = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        System.IO.File.WriteAllText(full, content);
        return full;
    }

    /// <summary>Creates a file below the root whose length is exactly <paramref name="bytes"/>.</summary>
    public string FileOfSize(string relativePath, int bytes) => File(relativePath, new string('x', bytes));

    /// <summary>Creates a file in <see cref="Outside"/>, i.e. somewhere the sandbox must never reach.</summary>
    public string OutsideFile(string relativePath, string content = "secret")
    {
        var full = Path.Combine(Outside, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        System.IO.File.WriteAllText(full, content);
        return full;
    }

    /// <summary>Creates a directory in <see cref="Outside"/>.</summary>
    public string OutsideDir(string relativePath)
    {
        var full = Path.Combine(Outside, relativePath);
        Directory.CreateDirectory(full);
        return full;
    }

    /// <summary>Links <paramref name="relativePath"/> below the root at <paramref name="target"/>.</summary>
    public void Symlink(string relativePath, string target, bool isDirectory) =>
        Link(Path.Combine(Root, relativePath), target, isDirectory);

    /// <summary>Links a path in <see cref="Outside"/>, for building a chain that leaves the root.</summary>
    public void OutsideSymlink(string relativePath, string target, bool isDirectory) =>
        Link(Path.Combine(Outside, relativePath), target, isDirectory);

    private static void Link(string linkPath, string target, bool isDirectory)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);

        if (isDirectory)
        {
            Directory.CreateSymbolicLink(linkPath, target);
        }
        else
        {
            System.IO.File.CreateSymbolicLink(linkPath, target);
        }
    }

    private static bool ProbeSymlinks()
    {
        var probe = Path.Combine(Path.GetTempPath(), "filehub-symlink-probe-" + Guid.NewGuid().ToString("N"));

        try
        {
            System.IO.File.CreateSymbolicLink(probe, Path.Combine(Path.GetTempPath(), "nowhere"));
            System.IO.File.Delete(probe);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        try
        {
            // Recursive delete removes a symlink rather than following it, so a link pointing at
            // Outside does not take the other half of the tree with it early.
            Directory.Delete(m_container, recursive: true);
        }
        catch (IOException)
        {
            // A test that deliberately left the tree in an odd state must not fail on cleanup.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: cleanup is best effort.
        }
    }
}
