using Dtos.Files;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// The shape of a listing, and what navigating and downloading do with a real directory tree.
/// </summary>
public sealed class FileListingTests : FilesTestBase
{
    [Fact]
    public async Task The_root_listing_shows_the_granted_base_paths()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root, "Movies");
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.GetBasePathsAsync(alice.Id);

        var entry = Assert.Single(result.Value);
        Assert.Equal(basePath.Id, entry.Id);
        Assert.Equal("Movies", entry.Name);
        Assert.True(entry.IsBasePath);
        Assert.True(entry.IsDir);
        Assert.Equal(string.Empty, entry.NextSegment);
    }

    [Fact]
    public async Task A_base_path_reports_its_entry_count_as_its_size()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        Tree.File("b.txt");
        Tree.Dir("sub");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.GetBasePathsAsync(alice.Id);

        Assert.Equal(3, Assert.Single(result.Value).Size);
    }

    [Fact]
    public async Task A_base_path_whose_directory_is_gone_is_skipped_rather_than_fatal()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var missing = Tree.Dir("removable");
        var basePath = await CreateBasePathAsync(missing, "Unplugged");
        var present = await CreateBasePathAsync(Tree.Dir("kept"), "Kept");
        await GrantAsync(basePath.Id, alice.Id);
        await GrantAsync(present.Id, alice.Id);
        Directory.Delete(missing);

        var result = await Files.GetBasePathsAsync(alice.Id);

        Assert.Equal("Kept", Assert.Single(result.Value).Name);
    }

    [Fact]
    public async Task Navigating_the_base_path_lists_its_entries()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        Tree.Dir("sub");
        var basePath = await CreateBasePathAsync(Tree.Root, "Movies");
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.NavigateAsync(alice.Id, new NavigateDto { BasePathId = basePath.Id, Path = string.Empty });

        Assert.True(result.IsSuccess);
        Assert.Equal("Movies", result.Value.NavigationName);
        Assert.Equal(["sub", "a.txt"], result.Value.Entries.Select(e => e.Name));
    }

    [Fact]
    public async Task A_file_entry_reports_its_size_in_bytes()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.FileOfSize("a.txt", 1234);
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.NavigateAsync(alice.Id, new NavigateDto { BasePathId = basePath.Id, Path = string.Empty });

        var entry = Assert.Single(result.Value.Entries);
        Assert.False(entry.IsDir);
        Assert.Equal(1234, entry.Size);
    }

    [Fact]
    public async Task A_directory_entry_reports_its_entry_count_as_its_size()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("sub/one.txt");
        Tree.File("sub/two.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.NavigateAsync(alice.Id, new NavigateDto { BasePathId = basePath.Id, Path = string.Empty });

        var entry = Assert.Single(result.Value.Entries);
        Assert.True(entry.IsDir);
        Assert.Equal(2, entry.Size);
    }

    [Fact]
    public async Task A_listed_entry_is_never_flagged_as_a_base_path()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.NavigateAsync(alice.Id, new NavigateDto { BasePathId = basePath.Id, Path = string.Empty });

        Assert.False(Assert.Single(result.Value.Entries).IsBasePath);
    }

    [Fact]
    public async Task A_next_segment_carries_no_leading_separator()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("sub/deeper/a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.NavigateAsync(alice.Id, new NavigateDto { BasePathId = basePath.Id, Path = "sub" });

        var entry = Assert.Single(result.Value.Entries);
        Assert.Equal(Path.Combine("sub", "deeper"), entry.NextSegment);
        Assert.False(entry.NextSegment.StartsWith(Path.DirectorySeparatorChar));
    }

    [Fact]
    public async Task A_next_segment_from_a_listing_navigates_straight_back_in()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("sub/deeper/a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);
        var root = await Files.NavigateAsync(alice.Id, new NavigateDto { BasePathId = basePath.Id, Path = string.Empty });

        var next = Assert.Single(root.Value.Entries).NextSegment;
        var result = await Files.NavigateAsync(alice.Id, new NavigateDto { BasePathId = basePath.Id, Path = next });

        Assert.True(result.IsSuccess);
        Assert.Equal("sub", result.Value.NavigationName);
    }

    [Fact]
    public async Task Navigating_into_a_subdirectory_names_the_navigation_after_it()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("sub/a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root, "Movies");
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.NavigateAsync(alice.Id, new NavigateDto { BasePathId = basePath.Id, Path = "sub" });

        Assert.Equal("sub", result.Value.NavigationName);
        Assert.Equal("a.txt", Assert.Single(result.Value.Entries).Name);
    }

    [Fact]
    public async Task Entries_are_listed_directories_first_then_by_name()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("zebra.txt");
        Tree.File("Apple.txt");
        Tree.Dir("zulu");
        Tree.Dir("Alpha");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.NavigateAsync(alice.Id, new NavigateDto { BasePathId = basePath.Id, Path = string.Empty });

        Assert.Equal(["Alpha", "zulu", "Apple.txt", "zebra.txt"], result.Value.Entries.Select(e => e.Name));
    }

    [Fact]
    public async Task Every_listing_gives_an_entry_a_fresh_item_id()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);
        var dto = new NavigateDto { BasePathId = basePath.Id, Path = string.Empty };

        var first = await Files.NavigateAsync(alice.Id, dto);
        var second = await Files.NavigateAsync(alice.Id, dto);

        // It is an in-listing identity only; persisting or comparing it across requests is what the
        // client must not do, and a stable id would invite exactly that.
        Assert.NotEqual(first.Value.Entries[0].ItemId, second.Value.Entries[0].ItemId);
    }

    [Fact]
    public async Task An_entry_that_symlinks_out_of_the_base_path_is_not_listed()
    {
        if (!TempTree.SymlinksSupported)
        {
            return;
        }

        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("visible.txt");
        Tree.Symlink("leak.txt", Tree.OutsideFile("secret.txt"), isDirectory: false);
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.NavigateAsync(alice.Id, new NavigateDto { BasePathId = basePath.Id, Path = string.Empty });

        // Filtered in the listing rather than 404ing on the download, so the browser never shows a
        // row that cannot be opened.
        Assert.Equal("visible.txt", Assert.Single(result.Value.Entries).Name);
    }

    [Fact]
    public async Task An_entry_that_cannot_be_read_is_skipped_rather_than_failing_the_listing()
    {
        if (!TempTree.SymlinksSupported)
        {
            return;
        }

        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("visible.txt");
        // A link to itself: it is inside the base path, it shows up in the directory, and every
        // attempt to look at it fails. One entry like that must not take the listing down with it.
        Tree.Symlink("loop", Path.Combine(Tree.Root, "loop"), isDirectory: false);
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.NavigateAsync(alice.Id, new NavigateDto { BasePathId = basePath.Id, Path = string.Empty });

        Assert.True(result.IsSuccess);
        Assert.Equal("visible.txt", Assert.Single(result.Value.Entries).Name);
    }

    [Fact]
    public async Task A_dotfile_is_listed_and_counted_like_any_other_entry()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.FileOfSize(".hidden", 12);
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var listed = await Files.NavigateAsync(alice.Id, new NavigateDto { BasePathId = basePath.Id, Path = string.Empty });
        var roots = await Files.GetBasePathsAsync(alice.Id);

        // The browser is a file manager for the host, not a shell: hiding entries here would make
        // the entry count on the base path disagree with what the listing shows.
        Assert.Equal(".hidden", Assert.Single(listed.Value.Entries).Name);
        Assert.Equal(1, Assert.Single(roots.Value).Size);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../outside")]
    [InlineData("sub/../../outside")]
    [InlineData("/etc")]
    public async Task Navigating_outside_the_base_path_is_not_found(string path)
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.Dir("sub");
        Tree.OutsideDir("outside");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.NavigateAsync(alice.Id, new NavigateDto { BasePathId = basePath.Id, Path = path });

        Assert.Equal(ResultCode.NotFound, result.ResultCode);
        Assert.Equal("Path not found", result.ErrorMessage);
    }

    [Fact]
    public async Task Navigating_to_a_directory_that_is_not_there_is_not_found()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.NavigateAsync(alice.Id, new NavigateDto { BasePathId = basePath.Id, Path = "nowhere" });

        Assert.Equal(ResultCode.NotFound, result.ResultCode);
    }

    [Fact]
    public async Task Navigating_to_a_file_is_not_found()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.NavigateAsync(alice.Id, new NavigateDto { BasePathId = basePath.Id, Path = "a.txt" });

        Assert.Equal(ResultCode.NotFound, result.ResultCode);
    }

    [Fact]
    public async Task Navigating_with_a_path_over_the_maximum_length_is_a_validation_error()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.NavigateAsync(
            alice.Id, new NavigateDto { BasePathId = basePath.Id, Path = new string('x', 4097) });

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(NavigateDto.Path), result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Downloading_a_file_resolves_it_below_the_base_path()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var file = Tree.File("sub/a.txt", "hello");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.ResolveDownloadAsync(alice.Id, basePath.Id, "sub/a.txt");

        Assert.True(result.IsSuccess);
        Assert.Equal(file, result.Value.FullPath);
        Assert.Equal("a.txt", result.Value.Name);
        Assert.False(result.Value.IsDirectory);
    }

    [Fact]
    public async Task Downloading_the_base_path_itself_uses_the_base_path_name()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root, "Movies");
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.ResolveDownloadAsync(alice.Id, basePath.Id, string.Empty);

        Assert.True(result.IsSuccess);
        Assert.Equal("Movies", result.Value.Name);
        Assert.True(result.Value.IsDirectory);
    }

    [Fact]
    public async Task Downloading_outside_the_base_path_is_not_found()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.OutsideFile("secret.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.ResolveDownloadAsync(alice.Id, basePath.Id, "../outside/secret.txt");

        Assert.Equal(ResultCode.NotFound, result.ResultCode);
        Assert.Equal("Path not found", result.ErrorMessage);
    }

    [Fact]
    public async Task Downloading_a_symlink_that_points_out_of_the_base_path_is_not_found()
    {
        if (!TempTree.SymlinksSupported)
        {
            return;
        }

        var alice = await CreateUserAsync("alice@example.com");
        Tree.Symlink("leak.txt", Tree.OutsideFile("secret.txt"), isDirectory: false);
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.ResolveDownloadAsync(alice.Id, basePath.Id, "leak.txt");

        Assert.Equal(ResultCode.NotFound, result.ResultCode);
    }

    [Fact]
    public async Task Downloading_a_file_that_is_not_there_is_not_found()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.ResolveDownloadAsync(alice.Id, basePath.Id, "gone.txt");

        Assert.Equal(ResultCode.NotFound, result.ResultCode);
    }

    [Fact]
    public async Task A_base_path_that_is_itself_a_symlink_can_still_be_browsed()
    {
        if (!TempTree.SymlinksSupported)
        {
            return;
        }

        // The realistic deployment shape: /data is a link to wherever the disk is actually mounted.
        // The containment check compares against the configured path, so the entries below it have
        // to keep resolving even though their real location is elsewhere.
        var alice = await CreateUserAsync("alice@example.com");
        var real = Tree.Dir("realdata");
        await File.WriteAllTextAsync(Path.Combine(real, "a.txt"), "hello");
        Tree.Symlink("mountpoint", real, isDirectory: true);
        var basePath = await CreateBasePathAsync(Path.Combine(Tree.Root, "mountpoint"), "Data");
        await GrantAsync(basePath.Id, alice.Id);

        var listed = await Files.NavigateAsync(alice.Id, new NavigateDto { BasePathId = basePath.Id, Path = string.Empty });
        var download = await Files.ResolveDownloadAsync(alice.Id, basePath.Id, "a.txt");

        Assert.Equal("a.txt", Assert.Single(listed.Value.Entries).Name);
        Assert.True(download.IsSuccess);
    }

    [Fact]
    public async Task Downloading_a_directory_resolves_it_as_a_directory()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("sub/a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.ResolveDownloadAsync(alice.Id, basePath.Id, "sub");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsDirectory);
        Assert.Equal("sub", result.Value.Name);
    }
}
