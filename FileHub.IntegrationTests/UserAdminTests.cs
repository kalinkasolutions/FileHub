using Dtos.Admin;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>Inviting, editing, disabling and deleting accounts.</summary>
public sealed class UserAdminTests : AdminTestBase
{
    [Fact]
    public async Task Inviting_creates_a_passwordless_unconfirmed_account_and_mails_the_link()
    {
        await EnsureRolesAsync();

        var result = await Admin.InviteUserAsync(new InviteUserDto
        {
            Username = "ada",
            Email = "ada@example.com",
            Roles = [Shared.Roles.User]
        });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.True(result.Value.InviteMailSent);

        var user = await UserManager.FindByIdAsync(result.Value.UserId.ToString());
        Assert.False(user!.EmailConfirmed);
        Assert.False(await UserManager.HasPasswordAsync(user));
        Assert.Equal(MailKind.Invite, Email.Last!.Kind);
        Assert.Equal("ada@example.com", Email.Last.Recipient);
    }

    [Fact]
    public async Task Inviting_always_adds_the_user_role()
    {
        await EnsureRolesAsync();

        var result = await Admin.InviteUserAsync(new InviteUserDto
        {
            Username = "ada",
            Email = "ada@example.com",
            Roles = []
        });

        // Browsing and sharing check for it, so an account without it could sign in and see nothing.
        var user = await UserManager.FindByIdAsync(result.Value.UserId.ToString());
        Assert.Equal([Shared.Roles.User], await UserManager.GetRolesAsync(user!));
    }

    [Fact]
    public async Task Inviting_an_admin_gives_the_account_both_roles()
    {
        await EnsureRolesAsync();

        var result = await Admin.InviteUserAsync(new InviteUserDto
        {
            Username = "ada",
            Email = "ada@example.com",
            Roles = [Shared.Roles.Admin]
        });

        var user = await UserManager.FindByIdAsync(result.Value.UserId.ToString());
        var roles = await UserManager.GetRolesAsync(user!);
        Assert.Contains(Shared.Roles.Admin, roles);
        Assert.Contains(Shared.Roles.User, roles);
    }

    [Fact]
    public async Task Inviting_matches_a_role_name_case_insensitively()
    {
        await EnsureRolesAsync();

        var result = await Admin.InviteUserAsync(new InviteUserDto
        {
            Username = "ada",
            Email = "ada@example.com",
            Roles = ["aDmIn"]
        });

        var user = await UserManager.FindByIdAsync(result.Value.UserId.ToString());
        Assert.Contains(Shared.Roles.Admin, await UserManager.GetRolesAsync(user!));
    }

    [Fact]
    public async Task Inviting_with_a_role_that_does_not_exist_is_rejected()
    {
        await EnsureRolesAsync();

        var result = await Admin.InviteUserAsync(new InviteUserDto
        {
            Username = "ada",
            Email = "ada@example.com",
            Roles = ["Superuser"]
        });

        // An invented role would grant nothing, so it is refused rather than silently dropped.
        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.Null(await UserManager.FindByEmailAsync("ada@example.com"));
    }

    [Fact]
    public async Task Inviting_an_address_that_already_has_an_account_is_rejected()
    {
        await CreateMemberAsync("ada@example.com");

        var result = await Admin.InviteUserAsync(new InviteUserDto
        {
            Username = "ada2",
            Email = "ada@example.com",
            Roles = [Shared.Roles.User]
        });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task Inviting_with_a_malformed_address_is_a_validation_error()
    {
        await EnsureRolesAsync();

        var result = await Admin.InviteUserAsync(new InviteUserDto
        {
            Username = "ada",
            Email = "not-an-address",
            Roles = [Shared.Roles.User]
        });

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(InviteUserDto.Email), result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Inviting_keeps_the_account_when_the_mail_cannot_be_sent()
    {
        await EnsureRolesAsync();
        Email.FailSends = true;

        var result = await Admin.InviteUserAsync(new InviteUserDto
        {
            Username = "ada",
            Email = "ada@example.com",
            Roles = [Shared.Roles.User]
        });

        // A failed delivery is an SMTP problem the admin fixes and resends from, not a reason to
        // unwind a created account — which is what InviteMailSent is for.
        Assert.True(result.IsSuccess);
        Assert.False(result.Value.InviteMailSent);
        Assert.NotNull(await UserManager.FindByEmailAsync("ada@example.com"));
    }

    [Fact]
    public async Task Listing_users_reports_their_roles_and_their_state()
    {
        var ada = await CreateAdminAsync("ada@example.com");
        await CreateMemberAsync("grace@example.com");

        var result = await Admin.ListUsersAsync();

        Assert.Equal(2, result.Value.Length);
        var listed = result.Value.Single(u => u.Id == ada.Id);
        Assert.Contains(Shared.Roles.Admin, listed.Roles);
        Assert.True(listed.EmailConfirmed);
        Assert.False(listed.IsLockedOut);
        Assert.False(listed.MustChangePassword);
    }

    [Fact]
    public async Task Listing_users_reports_a_disabled_account_as_locked_out()
    {
        var ada = await CreateAdminAsync("ada@example.com");
        var grace = await CreateMemberAsync("grace@example.com");
        await Admin.SetLockoutAsync(ada.Id, grace.Id, new SetLockoutDto { Locked = true });

        var result = await Admin.ListUsersAsync();

        Assert.True(result.Value.Single(u => u.Id == grace.Id).IsLockedOut);
    }

    // ---- editing ----

    [Fact]
    public async Task Updating_a_user_renames_them_and_replaces_their_roles()
    {
        await CreateAdminAsync("ada@example.com");
        var grace = await CreateMemberAsync("grace@example.com");

        var result = await Admin.UpdateUserAsync(grace.Id, new UpdateUserDto
        {
            Username = "Grace Hopper",
            Email = grace.Email!,
            Roles = [Shared.Roles.Admin]
        });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var reloaded = await ReloadAsync(grace.Id);
        Assert.Equal("Grace Hopper", reloaded.UserName);
        Assert.Contains(Shared.Roles.Admin, await UserManager.GetRolesAsync(reloaded));
    }

    [Fact]
    public async Task Updating_a_user_to_a_different_address_is_refused()
    {
        var grace = await CreateMemberAsync("grace@example.com");

        var result = await Admin.UpdateUserAsync(grace.Id, new UpdateUserDto
        {
            Username = grace.UserName!,
            Email = "somewhere-else@example.com",
            Roles = [Shared.Roles.User]
        });

        // Moving an account to a new address means proving the new address can be read, which only
        // its holder can do.
        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.Equal("grace@example.com", (await ReloadAsync(grace.Id)).Email);
    }

    [Fact]
    public async Task Updating_a_user_accepts_the_address_they_already_have_in_any_case()
    {
        var grace = await CreateMemberAsync("grace@example.com");

        var result = await Admin.UpdateUserAsync(grace.Id, new UpdateUserDto
        {
            Username = "Grace",
            Email = "GRACE@EXAMPLE.COM",
            Roles = [Shared.Roles.User]
        });

        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    [Fact]
    public async Task Updating_a_user_with_a_role_that_does_not_exist_is_rejected()
    {
        var grace = await CreateMemberAsync("grace@example.com");

        var result = await Admin.UpdateUserAsync(grace.Id, Unchanged(grace, "Superuser"));

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task Updating_a_user_keeps_the_user_role_even_when_it_is_left_out()
    {
        var grace = await CreateMemberAsync("grace@example.com");

        await Admin.UpdateUserAsync(grace.Id, Unchanged(grace));

        Assert.Equal([Shared.Roles.User], await UserManager.GetRolesAsync(await ReloadAsync(grace.Id)));
    }

    [Fact]
    public async Task Updating_an_unknown_user_is_not_found()
    {
        var result = await Admin.UpdateUserAsync(Guid.NewGuid(), new UpdateUserDto
        {
            Username = "ghost",
            Email = "ghost@example.com",
            Roles = [Shared.Roles.User]
        });

        Assert.Equal(ResultCode.NotFound, result.ResultCode);
    }

    [Fact]
    public async Task Updating_a_user_without_a_name_is_a_validation_error()
    {
        var grace = await CreateMemberAsync("grace@example.com");

        var result = await Admin.UpdateUserAsync(grace.Id, new UpdateUserDto
        {
            Username = "   ",
            Email = grace.Email!,
            Roles = [Shared.Roles.User]
        });

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(UpdateUserDto.Username), result.ValidationErrors.Keys);
    }

    // ---- lockout ----

    [Fact]
    public async Task Disabling_an_account_locks_it_out()
    {
        var ada = await CreateAdminAsync("ada@example.com");
        var grace = await CreateMemberAsync("grace@example.com");

        var result = await Admin.SetLockoutAsync(ada.Id, grace.Id, new SetLockoutDto { Locked = true });

        Assert.True(result.IsSuccess);
        var reloaded = await ReloadAsync(grace.Id);
        Assert.True(await UserManager.IsLockedOutAsync(reloaded));
        Assert.True(await UserManager.GetLockoutEnabledAsync(reloaded));
    }

    [Fact]
    public async Task Enabling_an_account_clears_the_lockout_and_the_failed_sign_in_count()
    {
        var ada = await CreateAdminAsync("ada@example.com");
        var grace = await CreateMemberAsync("grace@example.com");
        await Admin.SetLockoutAsync(ada.Id, grace.Id, new SetLockoutDto { Locked = true });
        await UserManager.AccessFailedAsync(grace);

        var result = await Admin.SetLockoutAsync(ada.Id, grace.Id, new SetLockoutDto { Locked = false });

        Assert.True(result.IsSuccess);
        var reloaded = await ReloadAsync(grace.Id);
        Assert.False(await UserManager.IsLockedOutAsync(reloaded));
        Assert.Equal(0, await UserManager.GetAccessFailedCountAsync(reloaded));
    }

    [Fact]
    public async Task An_admin_cannot_disable_their_own_account()
    {
        var ada = await CreateAdminAsync("ada@example.com");
        await CreateAdminAsync("grace@example.com");

        var result = await Admin.SetLockoutAsync(ada.Id, ada.Id, new SetLockoutDto { Locked = true });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.False(await UserManager.IsLockedOutAsync(await ReloadAsync(ada.Id)));
    }

    [Fact]
    public async Task An_admin_can_re_enable_their_own_account()
    {
        var ada = await CreateAdminAsync("ada@example.com");

        var result = await Admin.SetLockoutAsync(ada.Id, ada.Id, new SetLockoutDto { Locked = false });

        // Only disabling yourself is a foot-gun; the other direction is harmless.
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Disabling_an_unknown_user_is_not_found()
    {
        var ada = await CreateAdminAsync("ada@example.com");

        var result = await Admin.SetLockoutAsync(ada.Id, Guid.NewGuid(), new SetLockoutDto { Locked = true });

        Assert.Equal(ResultCode.NotFound, result.ResultCode);
    }

    // ---- deleting ----

    [Fact]
    public async Task An_admin_cannot_delete_their_own_account()
    {
        var ada = await CreateAdminAsync("ada@example.com");
        await CreateAdminAsync("grace@example.com");

        var result = await Admin.DeleteUserAsync(ada.Id, ada.Id);

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.NotNull(await UserManager.FindByIdAsync(ada.Id.ToString()));
    }

    [Fact]
    public async Task Deleting_a_user_removes_the_account()
    {
        var ada = await CreateAdminAsync("ada@example.com");
        var grace = await CreateMemberAsync("grace@example.com");

        var result = await Admin.DeleteUserAsync(ada.Id, grace.Id);

        Assert.True(result.IsSuccess);
        Context.ChangeTracker.Clear();
        Assert.Null(await UserManager.FindByIdAsync(grace.Id.ToString()));
    }

    [Fact]
    public async Task Deleting_an_unknown_user_is_not_found()
    {
        var ada = await CreateAdminAsync("ada@example.com");

        Assert.Equal(ResultCode.NotFound, (await Admin.DeleteUserAsync(ada.Id, Guid.NewGuid())).ResultCode);
    }

    [Fact]
    public async Task Deleting_a_user_removes_their_base_path_grants()
    {
        var ada = await CreateAdminAsync("ada@example.com");
        var grace = await CreateMemberAsync("grace@example.com");
        var basePath = new Entities.Paths.BasePath { Path = "/srv/media", Name = "Media" };
        Context.BasePaths.Add(basePath);
        Context.BasePathAccesses.Add(new Entities.Paths.BasePathAccess { BasePath = basePath, UserId = grace.Id });
        await Context.SaveChangesAsync();

        await Admin.DeleteUserAsync(ada.Id, grace.Id);

        Context.ChangeTracker.Clear();
        Assert.Empty(Context.BasePathAccesses);
    }

    // ---- the last-admin guards ----

    [Fact]
    public async Task The_last_active_admin_cannot_be_deleted()
    {
        var ada = await CreateAdminAsync("ada@example.com");
        var grace = await CreateMemberAsync("grace@example.com");

        var result = await Admin.DeleteUserAsync(grace.Id, ada.Id);

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.NotNull(await UserManager.FindByIdAsync(ada.Id.ToString()));
    }

    [Fact]
    public async Task The_last_active_admin_cannot_be_disabled()
    {
        var ada = await CreateAdminAsync("ada@example.com");
        var grace = await CreateMemberAsync("grace@example.com");

        var result = await Admin.SetLockoutAsync(grace.Id, ada.Id, new SetLockoutDto { Locked = true });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.False(await UserManager.IsLockedOutAsync(await ReloadAsync(ada.Id)));
    }

    [Fact]
    public async Task The_last_active_admin_cannot_be_demoted()
    {
        var ada = await CreateAdminAsync("ada@example.com");

        var result = await Admin.UpdateUserAsync(ada.Id, Unchanged(ada, Shared.Roles.User));

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.Contains(Shared.Roles.Admin, await UserManager.GetRolesAsync(await ReloadAsync(ada.Id)));
    }

    [Fact]
    public async Task An_admin_can_be_deleted_while_another_active_admin_remains()
    {
        var ada = await CreateAdminAsync("ada@example.com");
        var grace = await CreateAdminAsync("grace@example.com");

        var result = await Admin.DeleteUserAsync(grace.Id, ada.Id);

        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    [Fact]
    public async Task An_admin_can_be_demoted_while_another_active_admin_remains()
    {
        var ada = await CreateAdminAsync("ada@example.com");
        await CreateAdminAsync("grace@example.com");

        var result = await Admin.UpdateUserAsync(ada.Id, Unchanged(ada, Shared.Roles.User));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.DoesNotContain(Shared.Roles.Admin, await UserManager.GetRolesAsync(await ReloadAsync(ada.Id)));
    }

    [Fact]
    public async Task An_admin_who_never_accepted_their_invitation_does_not_count_as_the_remaining_admin()
    {
        var ada = await CreateAdminAsync("ada@example.com");
        var invited = await CreateAdminAsync("grace@example.com");
        invited.EmailConfirmed = false;
        await UserManager.UpdateAsync(invited);

        var result = await Admin.DeleteUserAsync(invited.Id, ada.Id);

        // They cannot sign in, so leaving only them behind locks the install out just as thoroughly
        // as leaving nobody behind.
        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task A_disabled_admin_does_not_count_as_the_remaining_admin()
    {
        var ada = await CreateAdminAsync("ada@example.com");
        var grace = await CreateAdminAsync("grace@example.com");
        await Admin.SetLockoutAsync(ada.Id, grace.Id, new SetLockoutDto { Locked = true });

        var result = await Admin.DeleteUserAsync(grace.Id, ada.Id);

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task The_last_admin_can_still_be_renamed()
    {
        var ada = await CreateAdminAsync("ada@example.com");

        var result = await Admin.UpdateUserAsync(ada.Id, new UpdateUserDto
        {
            Username = "Ada Lovelace",
            Email = ada.Email!,
            Roles = [Shared.Roles.Admin]
        });

        // The guard is about losing the role, not about touching the account.
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal("Ada Lovelace", (await ReloadAsync(ada.Id)).UserName);
    }

    [Fact]
    public async Task A_non_admin_is_never_treated_as_the_last_admin()
    {
        var ada = await CreateAdminAsync("ada@example.com");
        var grace = await CreateMemberAsync("grace@example.com");

        var result = await Admin.DeleteUserAsync(ada.Id, grace.Id);

        Assert.True(result.IsSuccess);
    }

    // ---- roles ----

    [Fact]
    public async Task Roles_are_listed_with_the_number_of_accounts_holding_them()
    {
        await CreateAdminAsync("ada@example.com");
        await CreateMemberAsync("grace@example.com");

        var result = await Roles.ListRolesAsync();

        Assert.Equal(2, result.Value.Length);
        Assert.Equal(1, result.Value.Single(r => r.Name == Shared.Roles.Admin).UserCount);
        Assert.Equal(2, result.Value.Single(r => r.Name == Shared.Roles.User).UserCount);
    }

    [Fact]
    public async Task Roles_are_listed_from_the_constants_even_on_an_empty_install()
    {
        var result = await Roles.ListRolesAsync();

        // They are what the authorization policies check, so a row that drifted from them would be
        // a lie — the list comes from the constants, not from the table.
        Assert.Equal([Shared.Roles.Admin, Shared.Roles.User], result.Value.Select(r => r.Name));
    }
}
