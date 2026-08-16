using Dtos.BasePaths;
using Dtos.Files;
using Dtos.Groups;
using Dtos.Shares;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// The whole access model. For an ordinary user it is the union of two grant tables: their own
/// <c>BasePathAccess</c> rows and the <c>BasePathGroupAccess</c> rows of every group they belong to.
/// Absence from both is a denial. The one wildcard is the <c>Admin</c> role, which is an implicit
/// grant of every base path — so these are the tests that say what "can reach it" means.
/// </summary>
public sealed class BasePathAccessTests : FilesTestBase
{
    [Fact]
    public async Task A_user_with_no_grant_sees_no_base_paths()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        await CreateBasePathAsync(Tree.Root);

        var result = await Files.GetBasePathsAsync(alice.Id, callerIsAdmin: false);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task A_user_with_no_grant_cannot_navigate()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);

        var result = await Files.NavigateAsync(alice.Id, callerIsAdmin: false, new NavigateDto { BasePathId = basePath.Id, Path = string.Empty });

        Assert.Equal(ResultCode.NotFound, result.ResultCode);
        Assert.Equal("Base path not found", result.ErrorMessage);
    }

    [Fact]
    public async Task A_user_with_no_grant_cannot_download()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);

        var result = await Files.ResolveDownloadAsync(alice.Id, callerIsAdmin: false, basePath.Id, "a.txt");

        Assert.Equal(ResultCode.NotFound, result.ResultCode);
        Assert.Equal("Base path not found", result.ErrorMessage);
    }

    [Fact]
    public async Task A_user_with_no_grant_cannot_share()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);

        var result = await Shares.CreateAsync(alice.Id, callerIsAdmin: false, new CreateShareDto
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

        var ungranted = await Files.NavigateAsync(alice.Id, callerIsAdmin: false, new NavigateDto { BasePathId = basePath.Id, Path = string.Empty });
        var unknown = await Files.NavigateAsync(alice.Id, callerIsAdmin: false, new NavigateDto { BasePathId = Guid.NewGuid(), Path = string.Empty });

        // Telling them apart would let a caller enumerate what other users can see.
        Assert.Equal(unknown.ResultCode, ungranted.ResultCode);
        Assert.Equal(unknown.ErrorMessage, ungranted.ErrorMessage);
    }

    // ---- the Admin role is an implicit grant of every base path ----

    [Fact]
    public async Task An_admin_sees_every_base_path_without_holding_a_grant()
    {
        var admin = await CreateUserAsync("admin@example.com", "test-password", Roles.Admin, Roles.User);
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);

        var listed = await Files.GetBasePathsAsync(admin.Id, callerIsAdmin: true);
        var navigated = await Files.NavigateAsync(
            admin.Id, callerIsAdmin: true, new NavigateDto { BasePathId = basePath.Id, Path = string.Empty });

        // The rule this replaces was the opposite: an admin used to see nothing until they granted
        // it to themselves. The grant tables are still the whole story for everyone else.
        Assert.Single(listed.Value);
        Assert.True(navigated.IsSuccess, navigated.ErrorMessage);
        Assert.Equal(0, basePath.UserCount);
    }

    [Fact]
    public async Task An_admin_can_download_from_a_base_path_they_hold_no_grant_for()
    {
        var admin = await CreateUserAsync("admin@example.com", "test-password", Roles.Admin, Roles.User);
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);

        var result = await Files.ResolveDownloadAsync(admin.Id, callerIsAdmin: true, basePath.Id, "a.txt");

        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    [Fact]
    public async Task An_admin_can_share_from_a_base_path_they_hold_no_grant_for()
    {
        var admin = await CreateUserAsync("admin@example.com", "test-password", Roles.Admin, Roles.User);
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);

        var result = await Shares.CreateAsync(admin.Id, callerIsAdmin: true, new CreateShareDto
        {
            BasePathId = basePath.Id,
            RelativePath = "a.txt"
        });

        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    [Fact]
    public async Task The_role_is_what_decides_it_not_the_account()
    {
        var admin = await CreateUserAsync("admin@example.com", "test-password", Roles.Admin, Roles.User);
        Tree.File("a.txt");
        await CreateBasePathAsync(Tree.Root);

        // Nothing below the endpoint reads the principal: callerIsAdmin is an argument all the way
        // down to the query. The same account with the flag off sees exactly what its grants say,
        // which is what makes a demoted admin lose the wildcard the moment their claim goes.
        Assert.Empty((await Files.GetBasePathsAsync(admin.Id, callerIsAdmin: false)).Value);
        Assert.Single((await Files.GetBasePathsAsync(admin.Id, callerIsAdmin: true)).Value);
    }

    // ---- a group grant is the other half of the union ----

    [Fact]
    public async Task A_group_grant_makes_a_base_path_visible_to_its_members()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root, "Movies");
        var group = await CreateGroupAsync("Family", alice.Id);

        await GrantToGroupsAsync(basePath.Id, group.Id);

        var listed = await Files.GetBasePathsAsync(alice.Id, callerIsAdmin: false);

        Assert.Equal("Movies", Assert.Single(listed.Value).Name);
    }

    [Fact]
    public async Task A_group_grant_lets_a_member_navigate_and_download()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("sub/a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        var group = await CreateGroupAsync("Family", alice.Id);
        await GrantToGroupsAsync(basePath.Id, group.Id);

        var navigated = await Files.NavigateAsync(
            alice.Id, callerIsAdmin: false, new NavigateDto { BasePathId = basePath.Id, Path = "sub" });
        var downloaded = await Files.ResolveDownloadAsync(alice.Id, callerIsAdmin: false, basePath.Id, "sub/a.txt");

        Assert.True(navigated.IsSuccess, navigated.ErrorMessage);
        Assert.True(downloaded.IsSuccess, downloaded.ErrorMessage);
    }

    [Fact]
    public async Task A_group_grant_reaches_only_the_members_of_that_group()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var bob = await CreateUserAsync("bob@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        var group = await CreateGroupAsync("Family", alice.Id);

        await GrantToGroupsAsync(basePath.Id, group.Id);

        Assert.Single((await Files.GetBasePathsAsync(alice.Id, callerIsAdmin: false)).Value);
        Assert.Empty((await Files.GetBasePathsAsync(bob.Id, callerIsAdmin: false)).Value);
    }

    [Fact]
    public async Task Access_is_the_union_of_the_users_own_grants_and_their_groups()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var movies = await CreateBasePathAsync(Tree.Dir("movies"), "Movies");
        var music = await CreateBasePathAsync(Tree.Dir("music"), "Music");
        var group = await CreateGroupAsync("Family", alice.Id);

        await GrantAsync(movies.Id, alice.Id);
        await GrantToGroupsAsync(music.Id, group.Id);

        var listed = await Files.GetBasePathsAsync(alice.Id, callerIsAdmin: false);

        Assert.Equal(["Movies", "Music"], listed.Value.Select(e => e.Name));
    }

    [Fact]
    public async Task A_base_path_granted_both_ways_is_listed_once()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        var group = await CreateGroupAsync("Family", alice.Id);

        await GrantAsync(basePath.Id, alice.Id);
        await GrantToGroupsAsync(basePath.Id, group.Id);

        // The union is one query over two tables, not two result sets stuck together.
        Assert.Single((await Files.GetBasePathsAsync(alice.Id, callerIsAdmin: false)).Value);
    }

    [Fact]
    public async Task Two_groups_granting_the_same_member_both_count()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var movies = await CreateBasePathAsync(Tree.Dir("movies"), "Movies");
        var music = await CreateBasePathAsync(Tree.Dir("music"), "Music");
        var family = await CreateGroupAsync("Family", alice.Id);
        var friends = await CreateGroupAsync("Friends", alice.Id);

        await GrantToGroupsAsync(movies.Id, family.Id);
        await GrantToGroupsAsync(music.Id, friends.Id);

        Assert.Equal(2, (await Files.GetBasePathsAsync(alice.Id, callerIsAdmin: false)).Value.Count);
    }

    [Fact]
    public async Task Revoking_a_groups_grant_hides_the_base_path_again()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        var group = await CreateGroupAsync("Family", alice.Id);
        await GrantToGroupsAsync(basePath.Id, group.Id);

        // The base path is now granted to no group at all.
        await GrantToGroupsAsync(basePath.Id);

        var navigated = await Files.NavigateAsync(
            alice.Id, callerIsAdmin: false, new NavigateDto { BasePathId = basePath.Id, Path = string.Empty });

        Assert.Empty((await Files.GetBasePathsAsync(alice.Id, callerIsAdmin: false)).Value);
        Assert.Equal(ResultCode.NotFound, navigated.ResultCode);
    }

    [Fact]
    public async Task Revoking_a_groups_grant_from_the_group_screen_hides_the_base_path_too()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        var group = await CreateGroupAsync("Family", alice.Id);
        await GrantToGroupsAsync(basePath.Id, group.Id);

        // The same grant table edited from the other end (api/admin/groups/{id}/base-paths).
        var result = await Groups.SetBasePathsAsync(group.Id, new SetGroupBasePathsDto { BasePathIds = [] });
        Assert.True(result.IsSuccess, result.ErrorMessage);

        Assert.Empty((await Files.GetBasePathsAsync(alice.Id, callerIsAdmin: false)).Value);
    }

    [Fact]
    public async Task Leaving_a_group_loses_what_it_granted()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var bob = await CreateUserAsync("bob@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        var group = await CreateGroupAsync("Family", alice.Id, bob.Id);
        await GrantToGroupsAsync(basePath.Id, group.Id);

        // The membership list is replaced, so alice is out.
        var result = await Groups.SetMembersAsync(group.Id, new SetGroupMembersDto { UserIds = [bob.Id] });
        Assert.True(result.IsSuccess, result.ErrorMessage);

        Assert.Empty((await Files.GetBasePathsAsync(alice.Id, callerIsAdmin: false)).Value);
        Assert.Single((await Files.GetBasePathsAsync(bob.Id, callerIsAdmin: false)).Value);
    }

    [Fact]
    public async Task Deleting_a_group_takes_the_access_it_granted()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        var group = await CreateGroupAsync("Family", alice.Id);
        await GrantToGroupsAsync(basePath.Id, group.Id);

        var result = await Groups.DeleteAsync(group.Id);
        Assert.True(result.IsSuccess, result.ErrorMessage);

        NewRequest();
        Assert.Empty((await Files.GetBasePathsAsync(alice.Id, callerIsAdmin: false)).Value);
    }

    [Fact]
    public async Task Deleting_a_user_takes_their_memberships()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        var group = await CreateGroupAsync("Family", alice.Id);
        await GrantToGroupsAsync(basePath.Id, group.Id);

        await UserManager.DeleteAsync(alice);

        // Both ends of GroupMembership cascade, so the row goes with the account.
        NewRequest();
        Assert.Empty((await Groups.GetMembersAsync(group.Id)).Value);
    }

    [Fact]
    public async Task Deleting_a_base_path_takes_its_group_grants()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        var group = await CreateGroupAsync("Family", alice.Id);
        await GrantToGroupsAsync(basePath.Id, group.Id);

        await BasePaths.DeleteAsync(basePath.Id);

        NewRequest();
        Assert.Empty((await Groups.GetBasePathsAsync(group.Id)).Value);
    }

    [Fact]
    public async Task Granting_a_base_path_makes_it_visible()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);

        await GrantAsync(basePath.Id, alice.Id);

        Assert.Single((await Files.GetBasePathsAsync(alice.Id, callerIsAdmin: false)).Value);
    }

    [Fact]
    public async Task Granting_a_base_path_lets_the_user_navigate_it()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);

        await GrantAsync(basePath.Id, alice.Id);

        var result = await Files.NavigateAsync(alice.Id, callerIsAdmin: false, new NavigateDto { BasePathId = basePath.Id, Path = string.Empty });
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

        Assert.Empty((await Files.GetBasePathsAsync(alice.Id, callerIsAdmin: false)).Value);
    }

    [Fact]
    public async Task Revoking_a_grant_stops_navigation()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        await GrantAsync(basePath.Id);

        var result = await Files.NavigateAsync(alice.Id, callerIsAdmin: false, new NavigateDto { BasePathId = basePath.Id, Path = string.Empty });
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

        Assert.Equal(ResultCode.NotFound, (await Files.ResolveDownloadAsync(alice.Id, callerIsAdmin: false, basePath.Id, "a.txt")).ResultCode);
    }

    [Fact]
    public async Task A_grant_to_one_user_is_not_a_grant_to_another()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var bob = await CreateUserAsync("bob@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);

        await GrantAsync(basePath.Id, alice.Id);

        Assert.Single((await Files.GetBasePathsAsync(alice.Id, callerIsAdmin: false)).Value);
        Assert.Empty((await Files.GetBasePathsAsync(bob.Id, callerIsAdmin: false)).Value);
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

        var result = await Files.GetBasePathsAsync(alice.Id, callerIsAdmin: false);

        Assert.Equal("Movies", Assert.Single(result.Value).Name);
    }
}
