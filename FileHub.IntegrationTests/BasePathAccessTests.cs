using Dtos.BasePaths;
using Dtos.Files;
using Dtos.Shares;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// The whole access model: absence of a <c>BasePathAccess</c> row is a denial, for everyone.
/// There is no wildcard and no role that gets in without one, so these are the tests that say what
/// "granted" means.
/// </summary>
public sealed class BasePathAccessTests : FilesTestBase
{
    [Fact]
    public async Task A_user_with_no_grant_sees_no_base_paths()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        await CreateBasePathAsync(Tree.Root);

        var result = await Files.GetBasePathsAsync(alice.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task A_user_with_no_grant_cannot_navigate()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);

        var result = await Files.NavigateAsync(alice.Id, new NavigateDto { BasePathId = basePath.Id, Path = string.Empty });

        Assert.Equal(ResultCode.NotFound, result.ResultCode);
        Assert.Equal("Base path not found", result.ErrorMessage);
    }

    [Fact]
    public async Task A_user_with_no_grant_cannot_download()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);

        var result = await Files.ResolveDownloadAsync(alice.Id, basePath.Id, "a.txt");

        Assert.Equal(ResultCode.NotFound, result.ResultCode);
        Assert.Equal("Base path not found", result.ErrorMessage);
    }

    [Fact]
    public async Task A_user_with_no_grant_cannot_share()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);

        var result = await Shares.CreateAsync(alice.Id, new CreateShareDto
        {
            BasePathId = basePath.Id,
            RelativePath = "a.txt"
        });

        Assert.Equal(ResultCode.NotFound, result.ResultCode);
        Assert.Empty(Context.Shares);
    }

    [Fact]
    public async Task A_base_path_nobody_holds_answers_exactly_like_one_that_does_not_exist()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);

        var ungranted = await Files.NavigateAsync(
            alice.Id, new NavigateDto { BasePathId = basePath.Id, Path = string.Empty });
        var unknown = await Files.NavigateAsync(
            alice.Id, new NavigateDto { BasePathId = Guid.NewGuid(), Path = string.Empty });

        // Telling them apart would let a caller enumerate what other users can see.
        Assert.Equal(unknown.ResultCode, ungranted.ResultCode);
        Assert.Equal(unknown.ErrorMessage, ungranted.ErrorMessage);
    }

    [Fact]
    public async Task An_admin_has_no_implicit_access()
    {
        var admin = await CreateUserAsync("admin@example.com", "test-password", Roles.Admin, Roles.User);
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);

        var listed = await Files.GetBasePathsAsync(admin.Id);
        var navigated = await Files.NavigateAsync(
            admin.Id, new NavigateDto { BasePathId = basePath.Id, Path = string.Empty });

        Assert.Empty(listed.Value);
        Assert.Equal(ResultCode.NotFound, navigated.ResultCode);
    }

    [Fact]
    public async Task An_admin_who_creates_a_base_path_is_not_granted_it()
    {
        var admin = await CreateUserAsync("admin@example.com", "test-password", Roles.Admin, Roles.User);
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);

        Assert.Equal(0, basePath.UserCount);
        Assert.Empty((await Files.GetBasePathsAsync(admin.Id)).Value);
    }

    [Fact]
    public async Task Granting_a_base_path_makes_it_visible()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);

        await GrantAsync(basePath.Id, alice.Id);

        Assert.Single((await Files.GetBasePathsAsync(alice.Id)).Value);
    }

    [Fact]
    public async Task Granting_a_base_path_lets_the_user_navigate_it()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);

        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.NavigateAsync(alice.Id, new NavigateDto { BasePathId = basePath.Id, Path = string.Empty });
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Revoking_a_grant_hides_the_base_path_again()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        await GrantAsync(basePath.Id);

        Assert.Empty((await Files.GetBasePathsAsync(alice.Id)).Value);
    }

    [Fact]
    public async Task Revoking_a_grant_stops_navigation()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        await GrantAsync(basePath.Id);

        var result = await Files.NavigateAsync(alice.Id, new NavigateDto { BasePathId = basePath.Id, Path = string.Empty });
        Assert.Equal(ResultCode.NotFound, result.ResultCode);
    }

    [Fact]
    public async Task Revoking_a_grant_stops_downloading()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        await GrantAsync(basePath.Id);

        Assert.Equal(ResultCode.NotFound, (await Files.ResolveDownloadAsync(alice.Id, basePath.Id, "a.txt")).ResultCode);
    }

    [Fact]
    public async Task A_grant_to_one_user_is_not_a_grant_to_another()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var bob = await CreateUserAsync("bob@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);

        await GrantAsync(basePath.Id, alice.Id);

        Assert.Single((await Files.GetBasePathsAsync(alice.Id)).Value);
        Assert.Empty((await Files.GetBasePathsAsync(bob.Id)).Value);
    }

    [Fact]
    public async Task Setting_the_users_of_a_base_path_replaces_the_previous_grants()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var bob = await CreateUserAsync("bob@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        await GrantAsync(basePath.Id, bob.Id);

        var users = await BasePaths.GetUsersAsync(basePath.Id);
        Assert.Equal(bob.Id, Assert.Single(users.Value));
    }

    [Fact]
    public async Task Granting_the_same_user_twice_stores_one_grant()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);

        await GrantAsync(basePath.Id, alice.Id, alice.Id);

        // The unique index on (BasePathId, UserId) would have refused the second row.
        Assert.Single((await BasePaths.GetUsersAsync(basePath.Id)).Value);
    }

    [Fact]
    public async Task Setting_the_base_paths_of_a_user_replaces_the_previous_grants()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var movies = await CreateBasePathAsync(Tree.Dir("movies"), "Movies");
        var music = await CreateBasePathAsync(Tree.Dir("music"), "Music");
        await GrantAsync(movies.Id, alice.Id);

        var result = await BasePaths.SetUserBasePathsAsync(
            alice.Id, new SetUserBasePathsDto { BasePathIds = [music.Id] });

        Assert.True(result.IsSuccess);
        Assert.Equal(music.Id, Assert.Single((await BasePaths.GetUserBasePathsAsync(alice.Id)).Value));
    }

    [Fact]
    public async Task Setting_the_base_paths_of_a_user_drops_an_unknown_base_path_id()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var movies = await CreateBasePathAsync(Tree.Dir("movies"), "Movies");

        var result = await BasePaths.SetUserBasePathsAsync(
            alice.Id, new SetUserBasePathsDto { BasePathIds = [movies.Id, Guid.NewGuid()] });

        // A stale id in the admin UI must not take the whole grant list down with it.
        Assert.True(result.IsSuccess);
        Assert.Equal(movies.Id, Assert.Single((await BasePaths.GetUserBasePathsAsync(alice.Id)).Value));
    }

    [Fact]
    public async Task A_user_sees_only_the_base_paths_they_hold()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var movies = await CreateBasePathAsync(Tree.Dir("movies"), "Movies");
        await CreateBasePathAsync(Tree.Dir("music"), "Music");
        await GrantAsync(movies.Id, alice.Id);

        var result = await Files.GetBasePathsAsync(alice.Id);

        Assert.Equal("Movies", Assert.Single(result.Value).Name);
    }
}
