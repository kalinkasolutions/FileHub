using Dtos.Account;
using Dtos.Auth;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// Account self-service: the password change that is the only way out of the forced-change gate,
/// and the two-phase email change whose second half runs signed out.
/// </summary>
public sealed class AccountTests : AccountTestBase
{
    private const string NewPassword = "brand-new-password";

    [Fact]
    public async Task Reading_the_account_reports_what_the_account_screen_shows()
    {
        var ada = await CreateAccountAsync();

        var result = await Account.GetAsync(ada.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(ada.Id, result.Value.UserId);
        Assert.Equal("ada@example.com", result.Value.Email);
        Assert.True(result.Value.EmailConfirmed);
        Assert.False(result.Value.TwoFactorEnabled);
        Assert.Equal(0, result.Value.RecoveryCodesLeft);
    }

    [Fact]
    public async Task Reading_an_unknown_account_is_not_found()
    {
        Assert.Equal(ResultCode.NotFound, (await Account.GetAsync(Guid.NewGuid())).ResultCode);
    }

    // ---- password ----

    [Fact]
    public async Task Changing_the_password_sets_the_new_one()
    {
        var ada = await CreateAccountAsync();

        var result = await Account.ChangePasswordAsync(ada.Id, Change(Password, NewPassword));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.True(await UserManager.CheckPasswordAsync(await ReloadAsync(ada.Id), NewPassword));
    }

    [Fact]
    public async Task Changing_the_password_clears_the_forced_password_change()
    {
        var ada = await CreateAccountAsync();
        ada.MustChangePassword = true;
        await UserManager.UpdateAsync(ada);

        await Account.ChangePasswordAsync(ada.Id, Change(Password, NewPassword));

        // The password is now the holder's own, so the gate comes down — it is the only way past it
        // for a session that is otherwise held to the account screen.
        Assert.False((await ReloadAsync(ada.Id)).MustChangePassword);
    }

    [Fact]
    public async Task Changing_the_password_with_the_wrong_current_one_is_rejected()
    {
        var ada = await CreateAccountAsync();

        var result = await Account.ChangePasswordAsync(ada.Id, Change("not-my-password", NewPassword));

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.True(await UserManager.CheckPasswordAsync(await ReloadAsync(ada.Id), Password));
    }

    [Fact]
    public async Task Changing_the_password_with_the_wrong_current_one_keeps_the_forced_change()
    {
        var ada = await CreateAccountAsync();
        ada.MustChangePassword = true;
        await UserManager.UpdateAsync(ada);

        await Account.ChangePasswordAsync(ada.Id, Change("not-my-password", NewPassword));

        Assert.True((await ReloadAsync(ada.Id)).MustChangePassword);
    }

    [Fact]
    public async Task Changing_the_password_with_a_mismatched_confirmation_is_a_validation_error()
    {
        var ada = await CreateAccountAsync();

        var result = await Account.ChangePasswordAsync(ada.Id, new ChangePasswordDto
        {
            CurrentPassword = Password,
            NewPassword = NewPassword,
            ConfirmPassword = "something-else"
        });

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(ChangePasswordDto.ConfirmPassword), result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Changing_the_password_to_one_below_the_minimum_length_is_a_validation_error()
    {
        var ada = await CreateAccountAsync();

        var result = await Account.ChangePasswordAsync(ada.Id, Change(Password, "short"));

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(ChangePasswordDto.NewPassword), result.ValidationErrors.Keys);
    }

    // ---- the two-phase email change ----

    [Fact]
    public async Task Requesting_an_email_change_mails_the_new_address_and_keeps_the_old_one()
    {
        var ada = await CreateAccountAsync();

        var result = await Account.RequestEmailChangeAsync(
            ada.Id, new ChangeEmailDto { Email = "ada@new.example.com", CurrentPassword = Password });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(MailKind.ChangeEmail, Email.Last!.Kind);
        Assert.Equal("ada@new.example.com", Email.Last.Recipient);
        // The account only moves once the user proves they can read mail there.
        Assert.Equal("ada@example.com", (await ReloadAsync(ada.Id)).Email);
    }

    [Fact]
    public async Task Confirming_an_email_change_moves_the_account_to_the_new_address()
    {
        var ada = await CreateAccountAsync();
        var mail = await RequestEmailChangeAsync(ada.Id, "ada@new.example.com");

        var result = await Identity.ConfirmEmailChangeAsync(new ConfirmEmailChangeDto
        {
            UserId = mail.UserId!.Value.ToString(),
            Email = "ada@new.example.com",
            Token = mail.Token
        });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var reloaded = await ReloadAsync(ada.Id);
        Assert.Equal("ada@new.example.com", reloaded.Email);
        Assert.True(reloaded.EmailConfirmed);
    }

    [Fact]
    public async Task Confirming_an_email_change_lets_the_new_address_find_the_account()
    {
        var ada = await CreateAccountAsync();
        var mail = await RequestEmailChangeAsync(ada.Id, "ada@new.example.com");
        await Identity.ConfirmEmailChangeAsync(new ConfirmEmailChangeDto
        {
            UserId = mail.UserId!.Value.ToString(),
            Email = "ada@new.example.com",
            Token = mail.Token
        });

        Context.ChangeTracker.Clear();

        Assert.Equal(ada.Id, (await UserManager.FindByEmailAsync("ada@new.example.com"))!.Id);
        Assert.Null(await UserManager.FindByEmailAsync("ada@example.com"));
    }

    [Fact]
    public async Task Requesting_an_email_change_with_the_wrong_password_is_rejected()
    {
        var ada = await CreateAccountAsync();

        var result = await Account.RequestEmailChangeAsync(
            ada.Id, new ChangeEmailDto { Email = "ada@new.example.com", CurrentPassword = "not-my-password" });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.Empty(Email.Sent);
    }

    [Fact]
    public async Task Requesting_an_email_change_to_the_current_address_is_rejected()
    {
        var ada = await CreateAccountAsync();

        var result = await Account.RequestEmailChangeAsync(
            ada.Id, new ChangeEmailDto { Email = "ADA@example.com", CurrentPassword = Password });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task Requesting_an_email_change_to_an_address_in_use_is_rejected()
    {
        var ada = await CreateAccountAsync();
        await CreateUserAsync("grace@example.com", Password);

        var result = await Account.RequestEmailChangeAsync(
            ada.Id, new ChangeEmailDto { Email = "grace@example.com", CurrentPassword = Password });

        // Caught here rather than after the token is redeemed, so the user gets a message they can
        // act on instead of a spent link.
        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.Empty(Email.Sent);
    }

    [Fact]
    public async Task Requesting_an_email_change_reports_a_failed_send()
    {
        var ada = await CreateAccountAsync();
        Email.FailSends = true;

        var result = await Account.RequestEmailChangeAsync(
            ada.Id, new ChangeEmailDto { Email = "ada@new.example.com", CurrentPassword = Password });

        // Without the mail the user has no way to complete the change, and nothing has moved yet.
        Assert.Equal(ResultCode.BadGateway, result.ResultCode);
        Assert.Equal("ada@example.com", (await ReloadAsync(ada.Id)).Email);
    }

    [Fact]
    public async Task Confirming_an_email_change_with_a_garbage_token_is_rejected()
    {
        var ada = await CreateAccountAsync();
        await RequestEmailChangeAsync(ada.Id, "ada@new.example.com");

        var result = await Identity.ConfirmEmailChangeAsync(new ConfirmEmailChangeDto
        {
            UserId = ada.Id.ToString(),
            Email = "ada@new.example.com",
            Token = "not-a-token"
        });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.Equal("ada@example.com", (await ReloadAsync(ada.Id)).Email);
    }

    [Fact]
    public async Task A_change_email_token_only_works_for_the_address_it_was_issued_for()
    {
        var ada = await CreateAccountAsync();
        var mail = await RequestEmailChangeAsync(ada.Id, "ada@new.example.com");

        var result = await Identity.ConfirmEmailChangeAsync(new ConfirmEmailChangeDto
        {
            UserId = mail.UserId!.Value.ToString(),
            Email = "attacker@example.com",
            Token = mail.Token
        });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.Equal("ada@example.com", (await ReloadAsync(ada.Id)).Email);
    }

    [Fact]
    public async Task A_change_email_token_cannot_be_used_twice()
    {
        var ada = await CreateAccountAsync();
        var mail = await RequestEmailChangeAsync(ada.Id, "ada@new.example.com");
        var dto = new ConfirmEmailChangeDto
        {
            UserId = mail.UserId!.Value.ToString(),
            Email = "ada@new.example.com",
            Token = mail.Token
        };
        await Identity.ConfirmEmailChangeAsync(dto);

        var result = await Identity.ConfirmEmailChangeAsync(dto);

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task Confirming_an_email_change_for_an_unknown_account_is_rejected()
    {
        var result = await Identity.ConfirmEmailChangeAsync(new ConfirmEmailChangeDto
        {
            UserId = Guid.NewGuid().ToString(),
            Email = "ada@new.example.com",
            Token = "whatever"
        });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task Confirming_an_email_change_with_a_malformed_user_id_is_rejected()
    {
        var result = await Identity.ConfirmEmailChangeAsync(new ConfirmEmailChangeDto
        {
            UserId = "not-a-guid",
            Email = "ada@new.example.com",
            Token = "whatever"
        });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    // ---- display name and sessions ----

    [Fact]
    public async Task Changing_the_display_name_stores_it_trimmed()
    {
        var ada = await CreateAccountAsync();

        var result = await Account.UpdateUsernameAsync(ada.Id, new UpdateUsernameDto { Username = "  Ada Lovelace  " });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal("Ada Lovelace", result.Value.Username);
        Assert.Equal("Ada Lovelace", (await ReloadAsync(ada.Id)).UserName);
    }

    [Fact]
    public async Task Changing_the_display_name_to_one_already_taken_is_rejected()
    {
        var ada = await CreateAccountAsync();
        var grace = await CreateUserAsync("grace@example.com", Password);

        var result = await Account.UpdateUsernameAsync(ada.Id, new UpdateUsernameDto { Username = grace.UserName! });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task Changing_the_display_name_to_the_one_it_already_has_is_a_no_op()
    {
        var ada = await CreateAccountAsync();

        var result = await Account.UpdateUsernameAsync(ada.Id, new UpdateUsernameDto { Username = ada.UserName! });

        Assert.True(result.IsSuccess);
        Assert.Equal(ada.UserName, result.Value.Username);
    }

    [Fact]
    public async Task Signing_out_everywhere_rotates_the_security_stamp()
    {
        var ada = await CreateAccountAsync();
        var before = await UserManager.GetSecurityStampAsync(ada);

        var result = await Account.SignOutEverywhereAsync(ada.Id);

        // The stamp is what every issued cookie is validated against, so rotating it is the sign-out.
        Assert.True(result.IsSuccess);
        Assert.NotEqual(before, await UserManager.GetSecurityStampAsync(await ReloadAsync(ada.Id)));
    }

    private static ChangePasswordDto Change(string current, string next) => new()
    {
        CurrentPassword = current,
        NewPassword = next,
        ConfirmPassword = next
    };

    private async Task<SentMail> RequestEmailChangeAsync(Guid userId, string email)
    {
        var result = await Account.RequestEmailChangeAsync(
            userId, new ChangeEmailDto { Email = email, CurrentPassword = Password });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        return Email.Last!;
    }
}
