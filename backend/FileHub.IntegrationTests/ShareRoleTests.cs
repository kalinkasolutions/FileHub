using Dtos.Shares;
using Microsoft.EntityFrameworkCore;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// Publishing a link is its own permission — the <c>CreateShares</c> role — separate from being able
/// to read the disk the link points into. These pin the two halves of that: who may create one, and
/// what happens to the links a user has already created when the permission goes away.
/// </summary>
public sealed class ShareRoleTests : SharesTestBase
{
    // ---- who may create a link ----

    [Fact]
    public async Task A_user_without_the_role_cannot_create_a_link()
    {
        var alice = await CreateUserAsync("alice@example.com", "test-password", Shared.Roles.User);
        Tree.FileOfSize("a.txt", 10);
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Shares.CreateAsync(
            alice.Id,
            callerIsAdmin: false,
            callerCanCreateShares: false,
            new CreateShareDto { BasePathId = basePath.Id, RelativePath = "a.txt" });

        Assert.Equal(ResultCode.Forbidden, result.ResultCode);
        Assert.Equal("Your account is not allowed to create share links", result.ErrorMessage);
        Assert.Empty(await Context.Shares.ToListAsync());
    }

    [Fact]
    public async Task Access_to_the_base_path_is_not_enough_on_its_own()
    {
        // The point of the role: Alice can browse and download every byte under this base path, and
        // still may not put an anonymous URL to it into the world.
        var alice = await CreateUserAsync("alice@example.com", "test-password", Shared.Roles.User);
        Tree.FileOfSize("a.txt", 10);
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var listing = await Files.NavigateAsync(alice.Id, callerIsAdmin: false, new Dtos.Files.NavigateDto
        {
            BasePathId = basePath.Id,
            Path = string.Empty
        });

        Assert.True(listing.IsSuccess, listing.ErrorMessage);

        var share = await Shares.CreateAsync(
            alice.Id,
            callerIsAdmin: false,
            callerCanCreateShares: false,
            new CreateShareDto { BasePathId = basePath.Id, RelativePath = "a.txt" });

        Assert.Equal(ResultCode.Forbidden, share.ResultCode);
    }

    [Fact]
    public async Task The_refusal_comes_before_the_path_is_looked_at()
    {
        // A caller who may not publish learns nothing about the base path or the path they named:
        // a base path they have no access to, and a file that is not there, answer the same
        // Forbidden as a legitimate target would.
        var alice = await CreateUserAsync("alice@example.com", "test-password", Shared.Roles.User);
        var basePath = await CreateBasePathAsync(Tree.Root);

        var result = await Shares.CreateAsync(
            alice.Id,
            callerIsAdmin: false,
            callerCanCreateShares: false,
            new CreateShareDto { BasePathId = basePath.Id, RelativePath = "nothing-here.txt" });

        Assert.Equal(ResultCode.Forbidden, result.ResultCode);
    }

    [Fact]
    public async Task A_user_with_the_role_can_create_a_link()
    {
        var alice = await CreateUserAsync(
            "alice@example.com", "test-password", Shared.Roles.User, Shared.Roles.CreateShares);
        Tree.FileOfSize("a.txt", 10);
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Shares.CreateAsync(
            alice.Id,
            callerIsAdmin: false,
            callerCanCreateShares: true,
            new CreateShareDto { BasePathId = basePath.Id, RelativePath = "a.txt" });

        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    [Fact]
    public async Task An_admin_can_create_a_link_without_holding_the_role()
    {
        // The Admin role implies every other one, so the service accepts an admin whose CreateShares
        // flag is false — which is also what a principal built by the claims factory would never be.
        var admin = await CreateUserAsync(
            "ada@example.com", "test-password", Shared.Roles.Admin, Shared.Roles.User);
        Tree.FileOfSize("a.txt", 10);
        var basePath = await CreateBasePathAsync(Tree.Root);

        var result = await Shares.CreateAsync(
            admin.Id,
            callerIsAdmin: true,
            callerCanCreateShares: false,
            new CreateShareDto { BasePathId = basePath.Id, RelativePath = "a.txt" });

        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    [Fact]
    public void The_role_is_seeded_and_implied_by_the_admin_role()
    {
        Assert.Contains(Shared.Roles.CreateShares, Shared.Roles.All);

        // Effective() is the one place the implication lives — the cookie and the status the SPA
        // reads are both built from it, so they cannot disagree.
        Assert.Contains(Shared.Roles.CreateShares, Shared.Roles.Effective([Shared.Roles.Admin]));
        Assert.DoesNotContain(Shared.Roles.CreateShares, Shared.Roles.Effective([Shared.Roles.User]));

        Assert.True(Shared.Roles.CanCreateShares([Shared.Roles.Admin]));
        Assert.True(Shared.Roles.CanCreateShares([Shared.Roles.User, Shared.Roles.CreateShares]));
        Assert.False(Shared.Roles.CanCreateShares([Shared.Roles.User]));
    }

    // ---- losing the role takes the links with it ----

    [Fact]
    public async Task Taking_the_role_away_revokes_the_links_that_user_created()
    {
        var alice = await CreateUserAsync(
            "alice@example.com", "test-password", Shared.Roles.User, Shared.Roles.CreateShares);
        Tree.FileOfSize("a.txt", 10);
        Tree.FileOfSize("b.txt", 10);
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);
        await ShareAsync(alice.Id, basePath.Id, "a.txt");
        await ShareAsync(alice.Id, basePath.Id, "b.txt");

        var result = await Users.UpdateUserAsync(alice.Id, Unchanged(alice, Shared.Roles.User));

        Assert.True(result.IsSuccess, result.ErrorMessage);

        NewRequest();
        Assert.Empty(await Context.Shares.ToListAsync());
    }

    [Fact]
    public async Task Keeping_the_role_leaves_the_links_alone()
    {
        var alice = await CreateUserAsync(
            "alice@example.com", "test-password", Shared.Roles.User, Shared.Roles.CreateShares);
        Tree.FileOfSize("a.txt", 10);
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);
        var share = await ShareAsync(alice.Id, basePath.Id, "a.txt");

        // A rename, with the roles as they were.
        var result = await Users.UpdateUserAsync(
            alice.Id,
            new Dtos.Admin.UpdateUserDto
            {
                Username = "Alice Renamed",
                Email = alice.Email!,
                Roles = [Shared.Roles.User, Shared.Roles.CreateShares]
            });

        Assert.True(result.IsSuccess, result.ErrorMessage);

        NewRequest();
        Assert.Equal(share.Id, (await Context.Shares.SingleAsync()).Id);
    }

    [Fact]
    public async Task Only_the_links_of_the_user_who_lost_the_role_are_revoked()
    {
        var alice = await CreateUserAsync(
            "alice@example.com", "test-password", Shared.Roles.User, Shared.Roles.CreateShares);
        var bob = await CreateUserAsync(
            "bob@example.com", "test-password", Shared.Roles.User, Shared.Roles.CreateShares);
        Tree.FileOfSize("a.txt", 10);
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id, bob.Id);
        await ShareAsync(alice.Id, basePath.Id, "a.txt");
        var bobsLink = await ShareAsync(bob.Id, basePath.Id, "a.txt");

        await Users.UpdateUserAsync(alice.Id, Unchanged(alice, Shared.Roles.User));

        NewRequest();
        var left = await Context.Shares.SingleAsync();
        Assert.Equal(bobsLink.Id, left.Id);
        Assert.Equal(bob.Id, left.CreatedById);
    }

    [Fact]
    public async Task Demoting_an_admin_revokes_the_links_they_published_under_the_wildcard()
    {
        // The Admin role is the third route to a base path and an implicit CreateShares, so a
        // demotion loses both at once. Bob's link points into a base path he was never granted: it
        // has to go, or an anonymous URL into it outlives every access he had.
        await CreateUserAsync("ada@example.com", "test-password", Shared.Roles.Admin, Shared.Roles.User);
        var bob = await CreateUserAsync(
            "bob@example.com", "test-password", Shared.Roles.Admin, Shared.Roles.User);
        Tree.FileOfSize("a.txt", 10);
        var basePath = await CreateBasePathAsync(Tree.Root);

        var created = await Shares.CreateAsync(
            bob.Id,
            callerIsAdmin: true,
            callerCanCreateShares: false,
            new CreateShareDto { BasePathId = basePath.Id, RelativePath = "a.txt" });

        Assert.True(created.IsSuccess, created.ErrorMessage);

        var result = await Users.UpdateUserAsync(bob.Id, Unchanged(bob, Shared.Roles.User));

        Assert.True(result.IsSuccess, result.ErrorMessage);

        NewRequest();
        Assert.Empty(await Context.Shares.ToListAsync());
    }

    [Fact]
    public async Task A_demoted_admin_who_keeps_the_share_role_keeps_their_links()
    {
        // Losing the wildcard is not losing the right to publish. The link is left for the
        // base-path revocation queries to judge, which is where access to a *path* is decided.
        await CreateUserAsync("ada@example.com", "test-password", Shared.Roles.Admin, Shared.Roles.User);
        var bob = await CreateUserAsync(
            "bob@example.com", "test-password", Shared.Roles.Admin, Shared.Roles.User);
        Tree.FileOfSize("a.txt", 10);
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, bob.Id);
        var share = await ShareAsync(bob.Id, basePath.Id, "a.txt");

        var result = await Users.UpdateUserAsync(
            bob.Id,
            Unchanged(bob, Shared.Roles.User, Shared.Roles.CreateShares));

        Assert.True(result.IsSuccess, result.ErrorMessage);

        NewRequest();
        Assert.Equal(share.Id, (await Context.Shares.SingleAsync()).Id);
    }

    [Fact]
    public async Task A_user_who_lost_the_role_can_still_list_and_revoke_what_is_left()
    {
        // Nothing is left after a role change, but a link can outlive the permission by other
        // routes — an admin deleting one by hand, a future direction we have not thought of. An
        // account that still holds a link must always be able to take it down.
        var alice = await CreateUserAsync("alice@example.com", "test-password", Shared.Roles.User);
        Tree.FileOfSize("a.txt", 10);
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);
        var share = await ShareAsync(alice.Id, basePath.Id, "a.txt");

        var listed = await Shares.ListForUserAsync(alice.Id);
        Assert.Equal(share.Id, Assert.Single(listed.Value).Id);

        var deleted = await Shares.DeleteAsync(alice.Id, callerIsAdmin: false, share.Id);
        Assert.True(deleted.IsSuccess, deleted.ErrorMessage);
    }

    /// <summary>The DTO the admin screen posts to leave an account as it is but for its roles.</summary>
    private static Dtos.Admin.UpdateUserDto Unchanged(Entities.Account.FileHubUser user, params string[] roles) =>
        new()
        {
            Username = user.UserName!,
            Email = user.Email!,
            Roles = roles
        };
}
