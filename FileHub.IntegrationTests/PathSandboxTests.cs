using FileHub.BusinessLogic.Authorization;

namespace FileHub.IntegrationTests;

/// <summary>
/// The highest-value tests in the suite. <see cref="PathSandbox"/> is the only place a
/// caller-supplied path becomes a path on disk, so a hole here is a remote read of the host
/// filesystem — every listing, download and share link goes through it.
/// </summary>
public sealed class PathSandboxTests : IDisposable
{
    private readonly TempTree m_tree;

    public PathSandboxTests()
    {
        m_tree = new TempTree();
    }

    // ---- the happy path ----

    [Fact]
    public void An_empty_relative_path_resolves_to_the_base_path_itself()
    {
        var resolved = PathSandbox.TryResolve(m_tree.Root, string.Empty, out var fullPath);

        Assert.True(resolved);
        Assert.Equal(m_tree.Root, fullPath);
    }

    [Fact]
    public void A_null_relative_path_resolves_to_the_base_path_itself()
    {
        var resolved = PathSandbox.TryResolve(m_tree.Root, null!, out var fullPath);

        Assert.True(resolved);
        Assert.Equal(m_tree.Root, fullPath);
    }

    [Fact]
    public void A_file_below_the_base_path_resolves()
    {
        var file = m_tree.File("notes.txt", "hello");

        var resolved = PathSandbox.TryResolve(m_tree.Root, "notes.txt", out var fullPath);

        Assert.True(resolved);
        Assert.Equal(file, fullPath);
    }

    [Fact]
    public void A_nested_path_resolves()
    {
        var file = m_tree.File("sub/deeper/notes.txt");

        var resolved = PathSandbox.TryResolve(m_tree.Root, "sub/deeper/notes.txt", out var fullPath);

        Assert.True(resolved);
        Assert.Equal(file, fullPath);
    }

    [Fact]
    public void A_path_that_is_not_on_disk_still_resolves()
    {
        // Existence is the caller's business: the sandbox answers "inside or outside", so a request
        // for a file that was just deleted is a 404 rather than an access refusal.
        var resolved = PathSandbox.TryResolve(m_tree.Root, "gone.txt", out var fullPath);

        Assert.True(resolved);
        Assert.Equal(Path.Combine(m_tree.Root, "gone.txt"), fullPath);
    }

    [Theory]
    [InlineData("sub/")]
    [InlineData("sub//")]
    [InlineData("./sub")]
    [InlineData("sub/./")]
    public void A_trailing_separator_or_dot_segment_resolves_to_the_same_path(string relativePath)
    {
        m_tree.Dir("sub");

        var resolved = PathSandbox.TryResolve(m_tree.Root, relativePath, out var fullPath);

        Assert.True(resolved);
        Assert.Equal(Path.Combine(m_tree.Root, "sub"), fullPath);
    }

    [Fact]
    public void A_single_dot_resolves_to_the_base_path()
    {
        var resolved = PathSandbox.TryResolve(m_tree.Root, ".", out var fullPath);

        Assert.True(resolved);
        Assert.Equal(m_tree.Root, fullPath);
    }

    [Fact]
    public void A_base_path_with_a_trailing_separator_is_normalized_away()
    {
        m_tree.File("notes.txt");

        var resolved = PathSandbox.TryResolve(m_tree.Root + Path.DirectorySeparatorChar, "notes.txt", out var fullPath);

        Assert.True(resolved);
        Assert.Equal(Path.Combine(m_tree.Root, "notes.txt"), fullPath);
    }

    // ---- climbing out ----

    [Theory]
    [InlineData("..")]
    [InlineData("../")]
    [InlineData("../..")]
    [InlineData("../outside/secret.txt")]
    [InlineData("sub/../../outside/secret.txt")]
    [InlineData("sub/../..")]
    [InlineData("./../outside")]
    public void A_climb_out_of_the_base_path_is_refused(string relativePath)
    {
        m_tree.Dir("sub");
        m_tree.OutsideFile("secret.txt");

        var resolved = PathSandbox.TryResolve(m_tree.Root, relativePath, out _);

        Assert.False(resolved);
    }

    [Fact]
    public void A_climb_out_of_the_base_path_is_refused_rather_than_clamped_back_onto_it()
    {
        // Clamping would answer a request for one file with a different file; the sandbox has to
        // leave the caller with nothing to act on.
        var resolved = PathSandbox.TryResolve(m_tree.Root, "../outside/secret.txt", out var fullPath);

        Assert.False(resolved);
        Assert.Equal(string.Empty, fullPath);
    }

    [Fact]
    public void A_climb_that_lands_back_inside_the_base_path_is_allowed()
    {
        var file = m_tree.File("sub/notes.txt");

        var resolved = PathSandbox.TryResolve(m_tree.Root, "sub/../sub/notes.txt", out var fullPath);

        Assert.True(resolved);
        Assert.Equal(file, fullPath);
    }

    [Fact]
    public void A_sibling_directory_whose_name_starts_with_the_base_path_is_outside()
    {
        // The containment check is a prefix comparison, so "/x/base-evil" must not read as being
        // under "/x/base".
        Directory.CreateDirectory(m_tree.Root + "-evil");

        var resolved = PathSandbox.TryResolve(m_tree.Root, "../base-evil/secret.txt", out _);

        Assert.False(resolved);
    }

    // ---- absolute and rooted input ----

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("/")]
    [InlineData(@"\\server\share")]
    [InlineData("C:\\Windows")]
    [InlineData("c:tmp")]
    public void A_rooted_or_drive_qualified_relative_path_is_refused(string relativePath)
    {
        var resolved = PathSandbox.TryResolve(m_tree.Root, relativePath, out var fullPath);

        Assert.False(resolved);
        Assert.Equal(string.Empty, fullPath);
    }

    [Fact]
    public void An_absolute_path_that_happens_to_be_inside_the_base_path_is_still_refused()
    {
        // One spelling per target: the wire format is "relative to the base path", full stop.
        var file = m_tree.File("notes.txt");

        var resolved = PathSandbox.TryResolve(m_tree.Root, file, out _);

        Assert.False(resolved);
    }

    [Fact]
    public void A_relative_path_with_an_embedded_null_is_refused()
    {
        var resolved = PathSandbox.TryResolve(m_tree.Root, "sub\0/etc", out var fullPath);

        Assert.False(resolved);
        Assert.Equal(string.Empty, fullPath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_base_path_refuses_everything(string basePath)
    {
        var resolved = PathSandbox.TryResolve(basePath, "notes.txt", out var fullPath);

        Assert.False(resolved);
        Assert.Equal(string.Empty, fullPath);
    }

    // ---- symlinks ----

    [Fact]
    public void A_symlinked_file_pointing_outside_the_base_path_is_refused()
    {
        if (!TempTree.SymlinksSupported)
        {
            return;
        }

        var secret = m_tree.OutsideFile("secret.txt");
        m_tree.Symlink("leak.txt", secret, isDirectory: false);

        var resolved = PathSandbox.TryResolve(m_tree.Root, "leak.txt", out var fullPath);

        Assert.False(resolved);
        Assert.Equal(string.Empty, fullPath);
    }

    [Fact]
    public void A_symlinked_directory_pointing_outside_the_base_path_is_refused()
    {
        if (!TempTree.SymlinksSupported)
        {
            return;
        }

        var elsewhere = m_tree.OutsideDir("elsewhere");
        m_tree.Symlink("leak", elsewhere, isDirectory: true);

        var resolved = PathSandbox.TryResolve(m_tree.Root, "leak", out _);

        Assert.False(resolved);
    }

    [Fact]
    public void A_path_below_a_symlinked_intermediate_directory_pointing_outside_is_refused()
    {
        if (!TempTree.SymlinksSupported)
        {
            return;
        }

        // The containment check on the resolved path cannot see this one: "base/leak/secret.txt" is
        // lexically inside the base path, and only the link on "leak" gives it away.
        var elsewhere = m_tree.OutsideDir("elsewhere");
        System.IO.File.WriteAllText(Path.Combine(elsewhere, "secret.txt"), "secret");
        m_tree.Symlink("leak", elsewhere, isDirectory: true);

        var resolved = PathSandbox.TryResolve(m_tree.Root, "leak/secret.txt", out var fullPath);

        Assert.False(resolved);
        Assert.Equal(string.Empty, fullPath);
    }

    [Fact]
    public void A_symlink_inside_a_base_path_that_is_itself_a_symlink_still_resolves()
    {
        if (!TempTree.SymlinksSupported)
        {
            return;
        }

        // The deployment case this protects: the admin registers /data, which is a link to the real
        // mount. A shortcut inside it resolves to the *mount's* path, so a root left unresolved
        // would read that as an escape and deny a file the user is plainly allowed to have.
        var real = m_tree.Dir("real");
        var target = m_tree.File("real/a.txt", "hello");
        var linkedRoot = Path.Combine(m_tree.Outside, "linked-root");
        Directory.CreateSymbolicLink(linkedRoot, real);
        System.IO.File.CreateSymbolicLink(Path.Combine(real, "shortcut.txt"), target);

        Assert.True(PathSandbox.TryResolve(linkedRoot, "a.txt", out _));
        // The accepted path is the one to open — the link itself, not its target: reading it is
        // what follows the link, and rewriting it here would change what the caller stores.
        Assert.True(PathSandbox.TryResolve(linkedRoot, "shortcut.txt", out var shortcut));
        Assert.EndsWith("shortcut.txt", shortcut, StringComparison.Ordinal);
        Assert.Equal("hello", System.IO.File.ReadAllText(shortcut));
    }

    [Fact]
    public void A_symlink_chain_that_ends_outside_the_base_path_is_refused()
    {
        if (!TempTree.SymlinksSupported)
        {
            return;
        }

        var secret = m_tree.OutsideFile("secret.txt");
        m_tree.OutsideSymlink("hop.txt", secret, isDirectory: false);
        m_tree.Symlink("leak.txt", Path.Combine(m_tree.Outside, "hop.txt"), isDirectory: false);

        var resolved = PathSandbox.TryResolve(m_tree.Root, "leak.txt", out _);

        Assert.False(resolved);
    }

    [Fact]
    public void A_symlinked_file_pointing_back_inside_the_base_path_is_allowed()
    {
        if (!TempTree.SymlinksSupported)
        {
            return;
        }

        var target = m_tree.File("real/notes.txt", "hello");
        m_tree.Symlink("shortcut.txt", target, isDirectory: false);

        var resolved = PathSandbox.TryResolve(m_tree.Root, "shortcut.txt", out var fullPath);

        Assert.True(resolved);
        Assert.Equal(Path.Combine(m_tree.Root, "shortcut.txt"), fullPath);
    }

    [Fact]
    public void A_symlinked_directory_pointing_back_inside_the_base_path_is_allowed()
    {
        if (!TempTree.SymlinksSupported)
        {
            return;
        }

        var target = m_tree.Dir("real");
        System.IO.File.WriteAllText(Path.Combine(target, "notes.txt"), "hello");
        m_tree.Symlink("shortcut", target, isDirectory: true);

        var resolved = PathSandbox.TryResolve(m_tree.Root, "shortcut/notes.txt", out var fullPath);

        Assert.True(resolved);
        Assert.Equal(Path.Combine(m_tree.Root, "shortcut", "notes.txt"), fullPath);
    }

    [Fact]
    public void A_dangling_symlink_pointing_outside_the_base_path_is_refused()
    {
        if (!TempTree.SymlinksSupported)
        {
            return;
        }

        // The target does not exist, so nothing is readable behind it today — but the link would
        // start working the moment that file appeared, and the sandbox refuses it now.
        m_tree.Symlink("dangling.txt", Path.Combine(m_tree.Outside, "never"), isDirectory: false);

        var resolved = PathSandbox.TryResolve(m_tree.Root, "dangling.txt", out var fullPath);

        Assert.False(resolved);
        Assert.Equal(string.Empty, fullPath);
    }

    [Fact]
    public void A_dangling_symlink_pointing_inside_the_base_path_resolves_and_is_left_to_the_caller()
    {
        if (!TempTree.SymlinksSupported)
        {
            return;
        }

        m_tree.Symlink("dangling.txt", Path.Combine(m_tree.Root, "never"), isDirectory: false);

        var resolved = PathSandbox.TryResolve(m_tree.Root, "dangling.txt", out var fullPath);

        // Containment is the sandbox's only question, and the answer is "inside". Whether the
        // target is really there is the caller's check, and it answers "not found".
        Assert.True(resolved);
        Assert.Equal(Path.Combine(m_tree.Root, "dangling.txt"), fullPath);
    }

    // ---- ToRelative ----

    [Fact]
    public void ToRelative_returns_the_path_below_the_base_path_without_a_leading_separator()
    {
        var file = m_tree.File("sub/notes.txt");

        var relative = PathSandbox.ToRelative(m_tree.Root, file);

        Assert.Equal(Path.Combine("sub", "notes.txt"), relative);
        Assert.False(relative.StartsWith(Path.DirectorySeparatorChar));
    }

    [Fact]
    public void ToRelative_returns_empty_for_the_base_path_itself()
    {
        Assert.Equal(string.Empty, PathSandbox.ToRelative(m_tree.Root, m_tree.Root));
    }

    [Fact]
    public void ToRelative_returns_empty_for_a_path_outside_the_base_path()
    {
        var secret = m_tree.OutsideFile("secret.txt");

        Assert.Equal(string.Empty, PathSandbox.ToRelative(m_tree.Root, secret));
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("sub/notes.txt")]
    [InlineData("sub/deeper/notes.txt")]
    [InlineData("")]
    public void ToRelative_round_trips_a_path_TryResolve_accepted(string relativePath)
    {
        if (relativePath.Length > 0)
        {
            m_tree.File(relativePath);
        }

        Assert.True(PathSandbox.TryResolve(m_tree.Root, relativePath, out var fullPath));

        var relative = PathSandbox.ToRelative(m_tree.Root, fullPath);

        Assert.Equal(relativePath.Replace('/', Path.DirectorySeparatorChar), relative);
        Assert.True(PathSandbox.TryResolve(m_tree.Root, relative, out var again));
        Assert.Equal(fullPath, again);
    }

    [Fact]
    public void ToRelative_normalizes_a_trailing_separator_on_the_base_path()
    {
        var file = m_tree.File("notes.txt");

        var relative = PathSandbox.ToRelative(m_tree.Root + Path.DirectorySeparatorChar, file);

        Assert.Equal("notes.txt", relative);
    }

    public void Dispose() => m_tree.Dispose();
}
