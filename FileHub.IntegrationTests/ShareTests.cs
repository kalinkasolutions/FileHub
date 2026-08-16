using Dtos.BasePaths;
using Dtos.Shares;
using Microsoft.EntityFrameworkCore;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// Public links: what creating one measures, what redeeming one is allowed to do, and what takes
/// one away again. The public half is the only unauthenticated surface in the app, so most of these
/// are about how little it does.
/// </summary>
public sealed class ShareTests : SharesTestBase
{
    // ---- creating ----

    [Fact]
    public async Task Creating_a_link_to_a_file_stores_its_size_in_bytes()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.FileOfSize("a.txt", 500);
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var share = await ShareAsync(alice.Id, basePath.Id, "a.txt");

        Assert.Equal(500, share.Size);
        Assert.False(share.IsDir);
        Assert.Equal("a.txt", share.Name);
    }

    [Fact]
    public async Task Creating_a_link_to_a_directory_measures_the_whole_tree()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.FileOfSize("sub/a.txt", 100);
        Tree.FileOfSize("sub/deeper/b.txt", 250);
        Tree.FileOfSize("elsewhere.txt", 9999);
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var share = await ShareAsync(alice.Id, basePath.Id, "sub");

        Assert.Equal(350, share.Size);
        Assert.True(share.IsDir);
    }

    [Fact]
    public async Task Creating_a_link_to_a_directory_ignores_a_symlink_inside_it()
    {
        if (!TempTree.SymlinksSupported)
        {
            return;
        }

        var alice = await CreateUserAsync("alice@example.com");
        Tree.FileOfSize("sub/a.txt", 100);
        // Following it would either count foreign bytes or count the same bytes twice.
        Tree.Symlink("sub/link.txt", Tree.OutsideFile("big.txt", new string('y', 5000)), isDirectory: false);
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var share = await ShareAsync(alice.Id, basePath.Id, "sub");

        Assert.Equal(100, share.Size);
    }

    [Fact]
    public async Task Creating_a_link_measures_the_size_once_and_caches_it_on_the_row()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var file = Tree.FileOfSize("a.txt", 10);
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);
        var share = await ShareAsync(alice.Id, basePath.Id, "a.txt");

        await File.WriteAllTextAsync(file, new string('x', 9000));
        NewRequest();
        var resolved = await Shares.ResolvePublicAsync(share.Id);

        // The public route is unauthenticated, so a fresh walk there would be free IO amplification
        // for anyone holding a link. It reports the stored measurement, stale or not.
        Assert.Equal(10, resolved.Value.Size);
    }

    [Fact]
    public async Task Creating_a_link_stores_the_relative_path_in_the_sandbox_normal_form()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("sub/a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var share = await ShareAsync(alice.Id, basePath.Id, "sub/./");

        // However the request was spelled, the link re-resolves to the same file.
        Assert.Equal("sub", share.RelativePath);
    }

    [Fact]
    public async Task Creating_a_link_to_the_base_path_itself_stores_an_empty_relative_path()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.FileOfSize("a.txt", 42);
        var basePath = await CreateBasePathAsync(Tree.Root, "Movies");
        await GrantAsync(basePath.Id, alice.Id);

        var share = await ShareAsync(alice.Id, basePath.Id, string.Empty);

        Assert.Equal(string.Empty, share.RelativePath);
        Assert.Equal("Movies", share.Name);
        Assert.Equal(42, share.Size);
    }

    [Fact]
    public async Task Creating_a_link_records_the_requested_download_limit()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var share = await ShareAsync(alice.Id, basePath.Id, "a.txt", maxDownloads: 3);

        Assert.Equal(3, share.MaxDownloadCount);
        Assert.Equal(0, share.DownloadCount);
    }

    [Fact]
    public async Task Creating_a_link_with_a_negative_download_limit_is_a_validation_error()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Shares.CreateAsync(alice.Id, new CreateShareDto
        {
            BasePathId = basePath.Id,
            RelativePath = "a.txt",
            MaxDownloadCount = -1
        });

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(CreateShareDto.MaxDownloadCount), result.ValidationErrors.Keys);
    }

    [Theory]
    [InlineData("../outside/secret.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("sub/../../outside/secret.txt")]
    public async Task Creating_a_link_to_a_path_outside_the_base_path_is_not_found(string relativePath)
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.Dir("sub");
        Tree.OutsideFile("secret.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Shares.CreateAsync(alice.Id, new CreateShareDto
        {
            BasePathId = basePath.Id,
            RelativePath = relativePath
        });

        Assert.Equal(ResultCode.NotFound, result.ResultCode);
        Assert.Empty(Context.Shares);
    }

    [Fact]
    public async Task Creating_a_link_to_a_path_that_is_not_there_is_not_found()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Shares.CreateAsync(alice.Id, new CreateShareDto
        {
            BasePathId = basePath.Id,
            RelativePath = "gone.txt"
        });

        Assert.Equal(ResultCode.NotFound, result.ResultCode);
    }

    // ---- redeeming ----

    [Fact]
    public async Task Resolving_a_link_returns_its_target()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var file = Tree.FileOfSize("sub/a.txt", 7);
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);
        var share = await ShareAsync(alice.Id, basePath.Id, "sub/a.txt");

        NewRequest();
        var result = await Shares.ResolvePublicAsync(share.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(file, result.Value.FullPath);
        Assert.Equal("a.txt", result.Value.Name);
        Assert.Equal(7, result.Value.Size);
        Assert.False(result.Value.IsDirectory);
    }

    [Fact]
    public async Task Resolving_an_unknown_link_is_not_found()
    {
        AssertPublicFailure(await Shares.ResolvePublicAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Resolving_a_link_whose_target_is_gone_is_not_found()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var file = Tree.File("a.txt", "hello");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);
        var share = await ShareAsync(alice.Id, basePath.Id, "a.txt");

        File.Delete(file);
        NewRequest();

        AssertPublicFailure(await Shares.ResolvePublicAsync(share.Id));
    }

    [Fact]
    public async Task Resolving_a_link_whose_target_became_a_symlink_out_is_not_found()
    {
        if (!TempTree.SymlinksSupported)
        {
            return;
        }

        var alice = await CreateUserAsync("alice@example.com");
        var file = Tree.File("a.txt", "hello");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);
        var share = await ShareAsync(alice.Id, basePath.Id, "a.txt");

        // The link is re-resolved through the sandbox on every hit, so the target turning into an
        // escape after the fact is caught rather than trusted.
        File.Delete(file);
        Tree.Symlink("a.txt", Tree.OutsideFile("secret.txt"), isDirectory: false);
        NewRequest();

        AssertPublicFailure(await Shares.ResolvePublicAsync(share.Id));
    }

    [Fact]
    public async Task Resolving_a_link_follows_its_base_path_after_it_is_repointed()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.FileOfSize("first/a.txt", 5);
        var second = Tree.Dir("second");
        await File.WriteAllTextAsync(Path.Combine(second, "a.txt"), "moved");
        var basePath = await CreateBasePathAsync(Path.Combine(Tree.Root, "first"), "Movies");
        await GrantAsync(basePath.Id, alice.Id);
        var share = await ShareAsync(alice.Id, basePath.Id, "a.txt");

        await BasePaths.UpdateAsync(basePath.Id, new SaveBasePathDto { Path = second, Name = "Movies" });
        NewRequest();
        var result = await Shares.ResolvePublicAsync(share.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(Path.Combine(second, "a.txt"), result.Value.FullPath);
    }

    [Fact]
    public async Task A_link_with_no_limit_keeps_resolving_however_often_it_is_used()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);
        var share = await ShareAsync(alice.Id, basePath.Id, "a.txt", maxDownloads: 0);

        for (var i = 0; i < 5; i++)
        {
            NewRequest();
            Assert.True((await Shares.ResolvePublicAsync(share.Id)).IsSuccess);
            await Shares.RegisterDownloadAsync(share.Id);
        }

        NewRequest();
        Assert.True((await Shares.ResolvePublicAsync(share.Id)).IsSuccess);
    }

    [Fact]
    public async Task A_link_stops_resolving_once_it_reaches_its_download_limit()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);
        var share = await ShareAsync(alice.Id, basePath.Id, "a.txt", maxDownloads: 2);

        for (var i = 0; i < 2; i++)
        {
            NewRequest();
            Assert.True((await Shares.ResolvePublicAsync(share.Id)).IsSuccess);
            await Shares.RegisterDownloadAsync(share.Id);
        }

        NewRequest();
        AssertPublicFailure(await Shares.ResolvePublicAsync(share.Id));
    }

    [Fact]
    public async Task Registering_a_download_counts_it_against_the_limit()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);
        var share = await ShareAsync(alice.Id, basePath.Id, "a.txt");

        await Shares.RegisterDownloadAsync(share.Id);
        await Shares.RegisterDownloadAsync(share.Id);

        NewRequest();
        Assert.Equal(2, (await Context.Shares.SingleAsync()).DownloadCount);
    }

    [Fact]
    public async Task Registering_a_download_of_an_unknown_link_does_nothing()
    {
        var result = await Shares.RegisterDownloadAsync(Guid.NewGuid());

        Assert.True(result.IsSuccess);
    }

    // ---- listing and revoking ----

    [Fact]
    public async Task A_link_is_listed_for_the_user_who_created_it()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var bob = await CreateUserAsync("bob@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id, bob.Id);
        var share = await ShareAsync(alice.Id, basePath.Id, "a.txt");

        Assert.Equal(share.Id, Assert.Single((await Shares.ListForUserAsync(alice.Id)).Value).Id);
        Assert.Empty((await Shares.ListForUserAsync(bob.Id)).Value);
    }

    [Fact]
    public async Task The_admin_list_shows_every_link_with_who_made_it()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root, "Movies");
        await GrantAsync(basePath.Id, alice.Id);
        await ShareAsync(alice.Id, basePath.Id, "a.txt");

        NewRequest();
        var result = await Shares.ListAllAsync();

        var listed = Assert.Single(result.Value);
        Assert.Equal(alice.Id, listed.CreatedById);
        Assert.Equal("alice@example.com", listed.CreatedBy);
        Assert.Equal("Movies", listed.BasePathName);
    }

    [Fact]
    public async Task A_user_can_revoke_their_own_link()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);
        var share = await ShareAsync(alice.Id, basePath.Id, "a.txt");

        var result = await Shares.DeleteAsync(alice.Id, callerIsAdmin: false, share.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(await Context.Shares.ToListAsync());
    }

    [Fact]
    public async Task A_user_cannot_revoke_someone_elses_link()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var bob = await CreateUserAsync("bob@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id, bob.Id);
        var share = await ShareAsync(alice.Id, basePath.Id, "a.txt");

        var result = await Shares.DeleteAsync(bob.Id, callerIsAdmin: false, share.Id);

        Assert.Equal(ResultCode.Forbidden, result.ResultCode);
        Assert.Single(await Context.Shares.ToListAsync());
    }

    [Fact]
    public async Task An_admin_can_revoke_any_link()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var admin = await CreateUserAsync("admin@example.com", "test-password", Roles.Admin, Roles.User);
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);
        var share = await ShareAsync(alice.Id, basePath.Id, "a.txt");

        var result = await Shares.DeleteAsync(admin.Id, callerIsAdmin: true, share.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(await Context.Shares.ToListAsync());
    }

    [Fact]
    public async Task Revoking_an_unknown_link_is_not_found()
    {
        var alice = await CreateUserAsync("alice@example.com");

        var result = await Shares.DeleteAsync(alice.Id, callerIsAdmin: true, Guid.NewGuid());

        Assert.Equal(ResultCode.NotFound, result.ResultCode);
    }

    [Fact]
    public async Task A_revoked_link_stops_resolving()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);
        var share = await ShareAsync(alice.Id, basePath.Id, "a.txt");

        await Shares.DeleteAsync(alice.Id, callerIsAdmin: false, share.Id);
        NewRequest();

        AssertPublicFailure(await Shares.ResolvePublicAsync(share.Id));
    }

    [Fact]
    public async Task A_link_keeps_working_after_its_creators_grant_is_revoked()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);
        var share = await ShareAsync(alice.Id, basePath.Id, "a.txt");

        await GrantAsync(basePath.Id);
        NewRequest();

        // Redeeming a link is anonymous by design: it is checked against the link, not against
        // whoever made it. Taking a link down means deleting it, or its base path.
        Assert.True((await Shares.ResolvePublicAsync(share.Id)).IsSuccess);
    }

    [Fact]
    public async Task Deleting_a_user_revokes_the_links_they_created()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var bob = await CreateUserAsync("bob@example.com");
        Tree.File("a.txt");
        Tree.File("b.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id, bob.Id);
        await ShareAsync(alice.Id, basePath.Id, "a.txt");
        var bobsShare = await ShareAsync(bob.Id, basePath.Id, "b.txt");

        await UserManager.DeleteAsync(alice);

        // An admin removing an account must not leave that account's public links alive.
        NewRequest();
        Assert.Equal(bobsShare.Id, (await Context.Shares.SingleAsync()).Id);
    }
}
