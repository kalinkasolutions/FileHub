using Dtos.Groups;
using Dtos.Shares;
using Microsoft.EntityFrameworkCore;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// Links aimed at a group. A link with no audience is anonymous by URL and behaves exactly as it
/// always has (<see cref="ShareTests"/>); one with an audience only answers a signed-in member of
/// it, and every refusal has to look like "no such link".
/// </summary>
public sealed class GroupShareTests : SharesTestBase
{
    // ---- creating ----

    [Fact]
    public async Task A_member_cannot_aim_a_link_at_a_group_they_belong_to()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);
        var family = await CreateGroupAsync("Family", alice.Id);

        var result = await Shares.CreateAsync(alice.Id, callerIsAdmin: false, callerCanCreateShares: true, new CreateShareDto
        {
            BasePathId = basePath.Id,
            RelativePath = "a.txt",
            AudienceGroupId = family.Id
        });

        // Being in the group is no longer enough, and belonging to it was the whole of the old
        // rule. A group-aimed link is read by members who may hold no route to the base path, so
        // creating one is an access decision — and those are the admin's.
        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.Empty(await Context.Shares.ToListAsync());
    }

    [Fact]
    public async Task A_link_with_no_audience_is_anonymous_by_url()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);

        var share = await ShareAsync(alice.Id, basePath.Id, "a.txt");

        Assert.Null(share.AudienceGroupId);
        Assert.Null(share.AudienceGroupName);

        NewRequest();
        Assert.True((await Shares.ResolvePublicAsync(share.Id, callerId: null, callerIsAdmin: false)).IsSuccess);
    }

    [Fact]
    public async Task A_non_member_cannot_aim_a_link_at_a_group()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var bob = await CreateUserAsync("bob@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id, bob.Id);
        var family = await CreateGroupAsync("Family", alice.Id);

        var result = await Shares.CreateAsync(bob.Id, callerIsAdmin: false, callerCanCreateShares: true, new CreateShareDto
        {
            BasePathId = basePath.Id,
            RelativePath = "a.txt",
            AudienceGroupId = family.Id
        });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.Empty(await Context.Shares.ToListAsync());
    }

    [Fact]
    public async Task A_real_group_and_an_invented_one_answer_a_non_admin_identically()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var bob = await CreateUserAsync("bob@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, bob.Id);
        var family = await CreateGroupAsync("Family", alice.Id);

        var real = await Shares.CreateAsync(bob.Id, callerIsAdmin: false, callerCanCreateShares: true, new CreateShareDto
        {
            BasePathId = basePath.Id,
            RelativePath = "a.txt",
            AudienceGroupId = family.Id
        });

        var invented = await Shares.CreateAsync(bob.Id, callerIsAdmin: false, callerCanCreateShares: true, new CreateShareDto
        {
            BasePathId = basePath.Id,
            RelativePath = "a.txt",
            AudienceGroupId = Guid.NewGuid()
        });

        // The refusal comes before the group is looked up at all, so the two cannot differ — which
        // is what stops the field being used to enumerate the groups in the install.
        Assert.Equal(ResultCode.BadRequest, real.ResultCode);
        Assert.Equal(invented.ResultCode, real.ResultCode);
        Assert.Equal(invented.ErrorMessage, real.ErrorMessage);
    }

    [Fact]
    public async Task An_unknown_group_answers_an_admin_the_same_way_too()
    {
        var admin = await CreateUserAsync("admin@example.com", "test-password", Roles.Admin, Roles.User);
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);

        var result = await Shares.CreateAsync(admin.Id, callerIsAdmin: true, callerCanCreateShares: true, new CreateShareDto
        {
            BasePathId = basePath.Id,
            RelativePath = "a.txt",
            AudienceGroupId = Guid.NewGuid()
        });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.Empty(await Context.Shares.ToListAsync());
    }

    [Fact]
    public async Task An_admin_can_aim_a_link_at_a_group_they_are_not_in()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var admin = await CreateUserAsync("admin@example.com", "test-password", Roles.Admin, Roles.User);
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        var family = await CreateGroupAsync("Family", alice.Id);

        var result = await Shares.CreateAsync(admin.Id, callerIsAdmin: true, callerCanCreateShares: true, new CreateShareDto
        {
            BasePathId = basePath.Id,
            RelativePath = "a.txt",
            AudienceGroupId = family.Id
        });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(family.Id, result.Value.AudienceGroupId);
    }

    // ---- redeeming ----

    [Fact]
    public async Task A_group_link_refuses_an_anonymous_caller()
    {
        var share = await GroupShareAsync();

        NewRequest();
        var result = await Shares.ResolvePublicAsync(share.Id, callerId: null, callerIsAdmin: false);

        // Exactly the answer an unknown id gets: a stranger must not learn that the link exists,
        // let alone which group it is for.
        AssertPublicFailure(result);
    }

    [Fact]
    public async Task A_group_link_refuses_a_signed_in_non_member()
    {
        var bob = await CreateUserAsync("bob@example.com");
        var share = await GroupShareAsync();

        NewRequest();
        AssertPublicFailure(await Shares.ResolvePublicAsync(share.Id, bob.Id, callerIsAdmin: false));
    }

    [Fact]
    public async Task A_group_link_answers_a_member()
    {
        var share = await GroupShareAsync();
        var alice = await Context.Users.SingleAsync(u => u.Email == "alice@example.com");

        NewRequest();
        var result = await Shares.ResolvePublicAsync(share.Id, alice.Id, callerIsAdmin: false);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal("a.txt", result.Value.Name);
    }

    [Fact]
    public async Task A_group_link_answers_an_admin()
    {
        var admin = await CreateUserAsync("admin@example.com", "test-password", Roles.Admin, Roles.User);
        var share = await GroupShareAsync();

        NewRequest();
        Assert.True((await Shares.ResolvePublicAsync(share.Id, admin.Id, callerIsAdmin: true)).IsSuccess);
    }

    [Fact]
    public async Task A_group_links_download_refuses_everyone_outside_the_group()
    {
        var bob = await CreateUserAsync("bob@example.com");
        var share = await GroupShareAsync();

        NewRequest();
        var anonymous = await Shares.RegisterDownloadAsync(share.Id, callerId: null, callerIsAdmin: false);
        NewRequest();
        var nonMember = await Shares.RegisterDownloadAsync(share.Id, bob.Id, callerIsAdmin: false);

        // The conditional UPDATE is the enforcement point, not the resolve that precedes it, so the
        // audience has to hold here on its own.
        Assert.Equal(ResultCode.NotFound, anonymous.ResultCode);
        Assert.Equal(ResultCode.NotFound, nonMember.ResultCode);

        NewRequest();
        Assert.Equal(0, (await Context.Shares.SingleAsync()).DownloadCount);
    }

    [Fact]
    public async Task A_group_links_download_counts_for_a_member()
    {
        var share = await GroupShareAsync();
        var alice = await Context.Users.SingleAsync(u => u.Email == "alice@example.com");

        NewRequest();
        Assert.True((await Shares.RegisterDownloadAsync(share.Id, alice.Id, callerIsAdmin: false)).IsSuccess);

        NewRequest();
        Assert.Equal(1, (await Context.Shares.SingleAsync()).DownloadCount);
    }

    [Fact]
    public async Task Leaving_the_audience_group_closes_the_link()
    {
        var share = await GroupShareAsync();
        var alice = await Context.Users.SingleAsync(u => u.Email == "alice@example.com");
        var family = await Context.Groups.SingleAsync();

        await Groups.SetMembersAsync(family.Id, new SetGroupMembersDto { UserIds = [] });

        // The audience is checked per request, not stamped on the row at creation time.
        NewRequest();
        AssertPublicFailure(await Shares.ResolvePublicAsync(share.Id, alice.Id, callerIsAdmin: false));
    }

    // ---- deleting the group ----

    [Fact]
    public async Task Deleting_a_group_deletes_the_links_aimed_at_it()
    {
        var share = await GroupShareAsync();
        var family = await Context.Groups.SingleAsync();

        await Groups.DeleteAsync(family.Id);

        // The foreign key cascades, so this cannot be forgotten by a service. If it only nulled the
        // column, deleting a group would turn a gated link into an anonymous one.
        NewRequest();
        Assert.Empty(await Context.Shares.ToListAsync());
        AssertPublicFailure(await Shares.ResolvePublicAsync(share.Id, callerId: null, callerIsAdmin: false));
    }

    [Fact]
    public async Task Deleting_a_group_leaves_the_links_that_were_not_aimed_at_it()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var owner = await CreateUserAsync("owner@example.com", "test-password", Roles.Admin, Roles.User);
        Tree.File("a.txt");
        Tree.File("b.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);
        var family = await CreateGroupAsync("Family", alice.Id);

        await ShareAsync(owner.Id, basePath.Id, "a.txt", audienceGroupId: family.Id, callerIsAdmin: true);
        var anonymous = await ShareAsync(alice.Id, basePath.Id, "b.txt");

        await Groups.DeleteAsync(family.Id);

        // Alice holds the base path in her own right, so her anonymous link is untouched.
        NewRequest();
        Assert.Equal(anonymous.Id, (await Context.Shares.SingleAsync()).Id);
    }

    // ---- a group grant revoked takes the links made under it ----

    [Fact]
    public async Task Revoking_a_groups_grant_takes_the_links_its_members_made_under_it()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        var family = await CreateGroupAsync("Family", alice.Id);
        await GrantToGroupsAsync(basePath.Id, family.Id);
        var share = await ShareAsync(alice.Id, basePath.Id, "a.txt");

        await GrantToGroupsAsync(basePath.Id);

        // Same reason a direct grant takes its links: the creator can no longer browse the base
        // path, and nothing on the redemption path would ever notice.
        NewRequest();
        AssertPublicFailure(await Shares.ResolvePublicAsync(share.Id, callerId: null, callerIsAdmin: false));
    }

    [Fact]
    public async Task Revoking_a_groups_grant_from_the_group_screen_takes_the_links_too()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        var family = await CreateGroupAsync("Family", alice.Id);
        await GrantToGroupsAsync(basePath.Id, family.Id);
        var share = await ShareAsync(alice.Id, basePath.Id, "a.txt");

        await Groups.SetBasePathsAsync(family.Id, new SetGroupBasePathsDto { BasePathIds = [] });

        NewRequest();
        AssertPublicFailure(await Shares.ResolvePublicAsync(share.Id, callerId: null, callerIsAdmin: false));
    }

    [Fact]
    public async Task Removing_a_member_takes_the_links_they_made_through_the_group()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var bob = await CreateUserAsync("bob@example.com");
        Tree.File("a.txt");
        Tree.File("b.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        var family = await CreateGroupAsync("Family", alice.Id, bob.Id);
        await GrantToGroupsAsync(basePath.Id, family.Id);
        var alices = await ShareAsync(alice.Id, basePath.Id, "a.txt");
        var bobs = await ShareAsync(bob.Id, basePath.Id, "b.txt");

        await Groups.SetMembersAsync(family.Id, new SetGroupMembersDto { UserIds = [bob.Id] });

        NewRequest();
        Assert.Equal(bobs.Id, (await Context.Shares.SingleAsync()).Id);
        AssertPublicFailure(await Shares.ResolvePublicAsync(alices.Id, callerId: null, callerIsAdmin: false));
    }

    [Fact]
    public async Task Revoking_a_groups_grant_leaves_a_link_its_creator_still_reaches_directly()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        var family = await CreateGroupAsync("Family", alice.Id);
        await GrantAsync(basePath.Id, alice.Id);
        await GrantToGroupsAsync(basePath.Id, family.Id);
        var share = await ShareAsync(alice.Id, basePath.Id, "a.txt");

        await GrantToGroupsAsync(basePath.Id);

        // Access is a union, so losing one of two routes to it is not a revocation.
        NewRequest();
        Assert.True((await Shares.ResolvePublicAsync(share.Id, callerId: null, callerIsAdmin: false)).IsSuccess);
    }

    [Fact]
    public async Task Revoking_a_direct_grant_leaves_a_link_its_creator_still_reaches_through_a_group()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        var family = await CreateGroupAsync("Family", alice.Id);
        await GrantAsync(basePath.Id, alice.Id);
        await GrantToGroupsAsync(basePath.Id, family.Id);
        var share = await ShareAsync(alice.Id, basePath.Id, "a.txt");

        await GrantAsync(basePath.Id);

        NewRequest();
        Assert.True((await Shares.ResolvePublicAsync(share.Id, callerId: null, callerIsAdmin: false)).IsSuccess);
    }

    [Fact]
    public async Task Revoking_every_grant_does_not_touch_an_admins_link()
    {
        var admin = await CreateUserAsync("admin@example.com", "test-password", Roles.Admin, Roles.User);
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);

        var created = await Shares.CreateAsync(admin.Id, callerIsAdmin: true, callerCanCreateShares: true, new CreateShareDto
        {
            BasePathId = basePath.Id,
            RelativePath = "a.txt"
        });
        Assert.True(created.IsSuccess, created.ErrorMessage);

        await GrantAsync(basePath.Id);

        // The Admin role is the third route to a base path, so a grant change never revokes it.
        NewRequest();
        Assert.True((await Shares.ResolvePublicAsync(created.Value.Id, callerId: null, callerIsAdmin: false)).IsSuccess);
    }

    // ---- the audience on the listings ----

    [Fact]
    public async Task The_creators_list_carries_the_audience()
    {
        var share = await GroupShareAsync();

        NewRequest();
        var owner = await Context.Users.SingleAsync(u => u.Email == "owner@example.com");
        var listed = Assert.Single((await Shares.ListForUserAsync(owner.Id)).Value);

        Assert.Equal(share.AudienceGroupId, listed.AudienceGroupId);
        Assert.Equal("Family", listed.AudienceGroupName);
    }

    [Fact]
    public async Task The_admin_list_carries_the_audience()
    {
        await GroupShareAsync();

        NewRequest();
        var listed = Assert.Single((await Shares.ListAllAsync()).Value);

        Assert.Equal("Family", listed.AudienceGroupName);
    }

    // ---- what my groups were sent ----

    [Fact]
    public async Task A_member_sees_the_link_aimed_at_their_group()
    {
        var share = await GroupShareAsync();

        NewRequest();
        var alice = await Context.Users.SingleAsync(u => u.Email == "alice@example.com");
        var listed = Assert.Single((await Shares.ListForAudienceAsync(alice.Id)).Value);

        Assert.Equal(share.Id, listed.Id);
        Assert.Equal("a.txt", listed.Name);
        Assert.Equal("Family", listed.AudienceGroupName);
        // Alice holds no grant on the base path — the link is her only route to the file, which is
        // the whole reason an admin aimed one at her group.
        Assert.Equal("owner", listed.SharedBy);
    }

    [Fact]
    public async Task The_received_list_leaves_the_path_off()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var owner = await CreateUserAsync("owner@example.com", "test-password", Roles.Admin, Roles.User);
        Tree.Dir("invoices");
        Tree.File(Path.Combine("invoices", "a.txt"), "hello");
        var basePath = await CreateBasePathAsync(Tree.Root);
        var family = await CreateGroupAsync("Family", alice.Id);

        await ShareAsync(
            owner.Id, basePath.Id, Path.Combine("invoices", "a.txt"),
            audienceGroupId: family.Id, callerIsAdmin: true);

        NewRequest();
        var listed = Assert.Single((await Shares.ListForAudienceAsync(alice.Id)).Value);

        // A recipient may hold no grant on the base path, so the DTO carries the target's name and
        // nothing that would sketch the directories above it.
        Assert.Equal("a.txt", listed.Name);
        Assert.DoesNotContain("invoices", System.Text.Json.JsonSerializer.Serialize(listed));
    }

    [Fact]
    public async Task A_non_member_sees_nothing_in_the_received_list()
    {
        var bob = await CreateUserAsync("bob@example.com");
        await GroupShareAsync();

        NewRequest();
        Assert.Empty((await Shares.ListForAudienceAsync(bob.Id)).Value);
    }

    [Fact]
    public async Task An_anonymous_link_is_in_nobodys_received_list()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("a.txt");
        var basePath = await CreateBasePathAsync(Tree.Root);
        await GrantAsync(basePath.Id, alice.Id);
        await CreateGroupAsync("Family", alice.Id);
        await ShareAsync(alice.Id, basePath.Id, "a.txt");

        // A link with no audience was not sent to anyone; it is in its creator's own list only.
        NewRequest();
        Assert.Empty((await Shares.ListForAudienceAsync(alice.Id)).Value);
        Assert.Single((await Shares.ListForUserAsync(alice.Id)).Value);
    }

    [Fact]
    public async Task An_admin_sees_only_what_their_own_groups_were_sent()
    {
        var admin = await CreateUserAsync("admin@example.com", "test-password", Roles.Admin, Roles.User);
        await GroupShareAsync();

        // The wildcard is a route to every base path, not a membership of every group: "what was
        // shared with me" is a fact about the caller's groups. Every link is in the admin list.
        NewRequest();
        Assert.Empty((await Shares.ListForAudienceAsync(admin.Id)).Value);
        Assert.Single((await Shares.ListAllAsync()).Value);
    }

    [Fact]
    public async Task Leaving_the_group_takes_the_link_out_of_the_received_list()
    {
        await GroupShareAsync();
        var alice = await Context.Users.SingleAsync(u => u.Email == "alice@example.com");
        var family = await Context.Groups.SingleAsync();

        await Groups.SetMembersAsync(family.Id, new SetGroupMembersDto { UserIds = [] });

        // The list is a live query over the memberships, the same fact the redemption checks — so
        // it cannot show a member a link they would then be refused.
        NewRequest();
        Assert.Empty((await Shares.ListForAudienceAsync(alice.Id)).Value);
    }

    /// <summary>
    /// One file, aimed at "Family", which alice is the only member of. The link is made by an admin
    /// — the only account that may aim one — reaching the base path by the wildcard, so nothing here
    /// depends on any grant, and alice is a plain member with no route to the base path of her own.
    /// That is the case the audience exists for.
    /// </summary>
    private async Task<ShareDto> GroupShareAsync()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var owner = await CreateUserAsync("owner@example.com", "test-password", Roles.Admin, Roles.User);
        Tree.File("a.txt", "hello");
        var basePath = await CreateBasePathAsync(Tree.Root);
        var family = await CreateGroupAsync("Family", alice.Id);

        return await ShareAsync(
            owner.Id, basePath.Id, "a.txt", audienceGroupId: family.Id, callerIsAdmin: true);
    }
}
