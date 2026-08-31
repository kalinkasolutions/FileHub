using Dtos.Groups;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// The admin surface for groups: creating, renaming, deleting, and editing the two lists a group
/// holds. The access these grants is covered by <see cref="BasePathAccessTests"/>; this file is
/// about the administration of the rows themselves.
/// </summary>
public sealed class GroupAdminTests : FilesTestBase
{
    [Fact]
    public async Task Creating_a_group_stores_it_with_no_members_and_no_base_paths()
    {
        var result = await Groups.CreateAsync(new SaveGroupDto { Name = "Family" });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal("Family", result.Value.Name);
        Assert.Equal(0, result.Value.MemberCount);
        Assert.Equal(0, result.Value.BasePathCount);
    }

    [Fact]
    public async Task Creating_a_group_trims_its_name()
    {
        var result = await Groups.CreateAsync(new SaveGroupDto { Name = "  Family  " });

        Assert.Equal("Family", result.Value.Name);
    }

    [Fact]
    public async Task Creating_a_group_with_a_blank_name_is_refused()
    {
        var result = await Groups.CreateAsync(new SaveGroupDto { Name = "   " });

        // [Required] trims before it decides, so whitespace never reaches the trim in the service.
        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(SaveGroupDto.Name), result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Creating_a_group_with_no_name_is_a_validation_error()
    {
        var result = await Groups.CreateAsync(new SaveGroupDto { Name = null });

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(SaveGroupDto.Name), result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task A_duplicate_group_name_is_a_clean_bad_request()
    {
        await CreateGroupAsync("Family");

        var result = await Groups.CreateAsync(new SaveGroupDto { Name = "Family" });

        // Not the unique index surfacing as a 500: this is a mistake an admin makes routinely.
        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.Contains("already a group", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Single((await Groups.ListAsync()).Value);
    }

    [Fact]
    public async Task A_duplicate_group_name_is_refused_whatever_its_case_or_padding()
    {
        await CreateGroupAsync("Family");

        var result = await Groups.CreateAsync(new SaveGroupDto { Name = "  family " });

        // The column is NOCASE, so the guard has to answer exactly what the index would refuse.
        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task Renaming_a_group_to_an_existing_name_is_a_clean_bad_request()
    {
        await CreateGroupAsync("Family");
        var friends = await CreateGroupAsync("Friends");

        var result = await Groups.RenameAsync(friends.Id, new SaveGroupDto { Name = "Family" });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task Renaming_a_group_to_its_own_name_is_allowed()
    {
        var family = await CreateGroupAsync("Family");

        var result = await Groups.RenameAsync(family.Id, new SaveGroupDto { Name = "Family" });

        // The uniqueness check has to exclude the row being edited, or saving a screen without
        // touching the name would fail.
        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    [Fact]
    public async Task Renaming_a_group_keeps_its_members_and_base_paths()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var basePath = await CreateBasePathAsync(Tree.Root);
        var family = await CreateGroupAsync("Family", alice.Id);
        await GrantToGroupsAsync(basePath.Id, family.Id);

        var result = await Groups.RenameAsync(family.Id, new SaveGroupDto { Name = "Household" });

        Assert.Equal("Household", result.Value.Name);
        Assert.Equal(1, result.Value.MemberCount);
        Assert.Equal(1, result.Value.BasePathCount);
    }

    [Fact]
    public async Task Renaming_an_unknown_group_is_not_found()
    {
        var result = await Groups.RenameAsync(Guid.NewGuid(), new SaveGroupDto { Name = "Family" });

        Assert.Equal(ResultCode.NotFound, result.ResultCode);
    }

    [Fact]
    public async Task The_group_list_carries_the_member_and_base_path_counts()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var bob = await CreateUserAsync("bob@example.com");
        var movies = await CreateBasePathAsync(Tree.Dir("movies"), "Movies");
        var music = await CreateBasePathAsync(Tree.Dir("music"), "Music");
        var family = await CreateGroupAsync("Family", alice.Id, bob.Id);
        await GrantToGroupsAsync(movies.Id, family.Id);
        await GrantToGroupsAsync(music.Id, family.Id);

        NewRequest();
        var listed = Assert.Single((await Groups.ListAsync()).Value);

        Assert.Equal(2, listed.MemberCount);
        Assert.Equal(2, listed.BasePathCount);
    }

    [Fact]
    public async Task The_group_list_is_in_name_order()
    {
        await CreateGroupAsync("Zoo");
        await CreateGroupAsync("Aviary");

        var result = await Groups.ListAsync();

        Assert.Equal(["Aviary", "Zoo"], result.Value.Select(g => g.Name));
    }

    [Fact]
    public async Task Setting_the_members_replaces_the_previous_ones()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var bob = await CreateUserAsync("bob@example.com");
        var family = await CreateGroupAsync("Family", alice.Id);

        await Groups.SetMembersAsync(family.Id, new SetGroupMembersDto { UserIds = [bob.Id] });

        Assert.Equal(bob.Id, Assert.Single((await Groups.GetMembersAsync(family.Id)).Value));
    }

    [Fact]
    public async Task Adding_the_same_member_twice_stores_one_membership()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var family = await CreateGroupAsync("Family");

        await Groups.SetMembersAsync(family.Id, new SetGroupMembersDto { UserIds = [alice.Id, alice.Id] });

        // The unique index on (GroupId, UserId) would have refused the second row.
        Assert.Single((await Groups.GetMembersAsync(family.Id)).Value);
    }

    [Fact]
    public async Task Setting_the_members_drops_an_unknown_user_id()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var family = await CreateGroupAsync("Family");

        var result = await Groups.SetMembersAsync(
            family.Id, new SetGroupMembersDto { UserIds = [alice.Id, Guid.NewGuid()] });

        // A stale id in the admin UI must not take the whole membership list down with it.
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(alice.Id, Assert.Single((await Groups.GetMembersAsync(family.Id)).Value));
    }

    [Fact]
    public async Task Setting_the_base_paths_drops_an_unknown_base_path_id()
    {
        var movies = await CreateBasePathAsync(Tree.Dir("movies"), "Movies");
        var family = await CreateGroupAsync("Family");

        var result = await Groups.SetBasePathsAsync(
            family.Id, new SetGroupBasePathsDto { BasePathIds = [movies.Id, Guid.NewGuid()] });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(movies.Id, Assert.Single((await Groups.GetBasePathsAsync(family.Id)).Value));
    }

    [Fact]
    public async Task The_two_ends_of_a_group_grant_are_the_same_table()
    {
        var basePath = await CreateBasePathAsync(Tree.Root);
        var family = await CreateGroupAsync("Family");

        await GrantToGroupsAsync(basePath.Id, family.Id);

        // Set from the base-path end, read from the group end.
        Assert.Equal(basePath.Id, Assert.Single((await Groups.GetBasePathsAsync(family.Id)).Value));
        Assert.Equal(family.Id, Assert.Single((await BasePaths.GetGroupsAsync(basePath.Id)).Value));
    }

    [Fact]
    public async Task The_base_path_list_counts_the_groups_granted_it()
    {
        var basePath = await CreateBasePathAsync(Tree.Root);
        var family = await CreateGroupAsync("Family");
        await GrantToGroupsAsync(basePath.Id, family.Id);

        NewRequest();
        var listed = Assert.Single((await BasePaths.GetAllAsync()).Value);

        Assert.Equal(0, listed.UserCount);
        Assert.Equal(1, listed.GroupCount);
    }

    [Fact]
    public async Task Members_and_base_paths_of_an_unknown_group_are_not_found()
    {
        var unknown = Guid.NewGuid();

        Assert.Equal(ResultCode.NotFound, (await Groups.GetMembersAsync(unknown)).ResultCode);
        Assert.Equal(ResultCode.NotFound, (await Groups.GetBasePathsAsync(unknown)).ResultCode);
        Assert.Equal(ResultCode.NotFound, (await Groups.DeleteAsync(unknown)).ResultCode);
    }

    // ---- what the caller may aim a share at ----

    [Fact]
    public async Task A_user_is_offered_only_their_own_groups()
    {
        var alice = await CreateUserAsync("alice@example.com");
        await CreateGroupAsync("Family", alice.Id);
        await CreateGroupAsync("Colleagues");

        var result = await Groups.ListForCallerAsync(alice.Id, callerIsAdmin: false);

        Assert.Equal("Family", Assert.Single(result.Value).Name);
    }

    [Fact]
    public async Task An_admin_is_offered_every_group()
    {
        var admin = await CreateUserAsync("admin@example.com", "test-password", Roles.Admin, Roles.User);
        await CreateGroupAsync("Family");
        await CreateGroupAsync("Colleagues");

        var result = await Groups.ListForCallerAsync(admin.Id, callerIsAdmin: true);

        Assert.Equal(2, result.Value.Count);
    }
}
