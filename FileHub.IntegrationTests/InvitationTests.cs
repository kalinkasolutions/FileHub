using Dtos.Auth;
using Microsoft.AspNetCore.Identity;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// The invitation round trip, end to end: an admin creates the account, the mail carries the token,
/// and following the link is what gives the account its password and its confirmed address. It is
/// the only way an account ever becomes usable, so a hole here is either a locked-out install or an
/// account someone else can claim.
/// </summary>
public sealed class InvitationTests : IdentityTestBase
{
    [Fact]
    public async Task An_invitation_creates_an_account_that_cannot_sign_in_yet()
    {
        var mail = await InviteAsync();

        var user = await ReloadAsync(mail.UserId!.Value);

        Assert.False(user.EmailConfirmed);
        Assert.False(await UserManager.HasPasswordAsync(user));
        Assert.Equal(SignInResult.NotAllowed, await SignIn.CheckPasswordSignInAsync(user, Password, false));
    }

    [Fact]
    public async Task An_invitation_is_mailed_to_the_invited_address()
    {
        var mail = await InviteAsync(email: "ada@example.com");

        Assert.Equal("ada@example.com", mail.Recipient);
        Assert.False(string.IsNullOrEmpty(mail.Token));
    }

    [Fact]
    public async Task Accepting_an_invitation_sets_the_password_and_confirms_the_address()
    {
        var mail = await InviteAsync();

        var result = await Identity.AcceptInviteAsync(new AcceptInviteDto
        {
            UserId = mail.UserId!.Value.ToString(),
            Token = mail.Token,
            Password = Password,
            DisplayName = string.Empty
        });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var user = await ReloadAsync(mail.UserId.Value);
        Assert.True(user.EmailConfirmed);
        Assert.True(await UserManager.CheckPasswordAsync(user, Password));
    }

    [Fact]
    public async Task Accepting_an_invitation_lets_the_account_sign_in()
    {
        var mail = await InviteAsync();
        await AcceptAsync(mail);

        var user = await ReloadAsync(mail.UserId!.Value);

        Assert.Equal(SignInResult.Success, await SignIn.CheckPasswordSignInAsync(user, Password, false));
    }

    [Fact]
    public async Task Accepting_an_invitation_applies_the_display_name_the_invitee_typed()
    {
        var mail = await InviteAsync(username: "ada");

        await Identity.AcceptInviteAsync(new AcceptInviteDto
        {
            UserId = mail.UserId!.Value.ToString(),
            Token = mail.Token,
            Password = Password,
            DisplayName = "  Ada Lovelace  "
        });

        Assert.Equal("Ada Lovelace", (await ReloadAsync(mail.UserId.Value)).UserName);
    }

    [Fact]
    public async Task Accepting_an_invitation_without_a_display_name_keeps_the_one_the_admin_typed()
    {
        var mail = await InviteAsync(username: "ada");

        await AcceptAsync(mail);

        Assert.Equal("ada", (await ReloadAsync(mail.UserId!.Value)).UserName);
    }

    [Fact]
    public async Task Accepting_an_invitation_still_activates_the_account_when_the_display_name_is_taken()
    {
        await CreateUserAsync("grace@example.com");
        var mail = await InviteAsync(username: "ada", email: "ada@example.com");

        var result = await Identity.AcceptInviteAsync(new AcceptInviteDto
        {
            UserId = mail.UserId!.Value.ToString(),
            Token = mail.Token,
            Password = Password,
            DisplayName = "grace"
        });

        // The link is spent by this point, so failing the whole call would leave the invitee with
        // no way back in over a name they can change from the account screen.
        Assert.True(result.IsSuccess);
        Assert.Equal("ada", (await ReloadAsync(mail.UserId.Value)).UserName);
    }

    [Fact]
    public async Task Accepting_an_invitation_clears_the_forced_password_change()
    {
        var mail = await InviteAsync();
        await SetMustChangePasswordAsync(mail.UserId!.Value);

        await AcceptAsync(mail);

        Assert.False((await ReloadAsync(mail.UserId.Value)).MustChangePassword);
    }

    [Fact]
    public async Task An_invitation_token_cannot_be_used_twice()
    {
        var mail = await InviteAsync();
        await AcceptAsync(mail);

        var result = await Identity.AcceptInviteAsync(new AcceptInviteDto
        {
            UserId = mail.UserId!.Value.ToString(),
            Token = mail.Token,
            Password = "second-password",
            DisplayName = string.Empty
        });

        // Setting the first password rotated the security stamp the token is bound to.
        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.True(await UserManager.CheckPasswordAsync(await ReloadAsync(mail.UserId.Value), Password));
    }

    [Fact]
    public async Task A_garbage_invitation_token_is_rejected()
    {
        var mail = await InviteAsync();

        var result = await Identity.AcceptInviteAsync(new AcceptInviteDto
        {
            UserId = mail.UserId!.Value.ToString(),
            Token = "not-a-token",
            Password = Password,
            DisplayName = string.Empty
        });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.False(await UserManager.HasPasswordAsync(await ReloadAsync(mail.UserId.Value)));
    }

    [Fact]
    public async Task An_invitation_token_issued_for_another_account_is_rejected()
    {
        var ada = await InviteAsync(username: "ada", email: "ada@example.com");
        var grace = await InviteAsync(username: "grace", email: "grace@example.com");

        var result = await Identity.AcceptInviteAsync(new AcceptInviteDto
        {
            UserId = ada.UserId!.Value.ToString(),
            Token = grace.Token,
            Password = Password,
            DisplayName = string.Empty
        });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task An_invitation_for_an_unknown_account_is_rejected()
    {
        var result = await Identity.AcceptInviteAsync(new AcceptInviteDto
        {
            UserId = Guid.NewGuid().ToString(),
            Token = "whatever",
            Password = Password,
            DisplayName = string.Empty
        });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task A_malformed_user_id_on_an_invitation_is_rejected()
    {
        var result = await Identity.AcceptInviteAsync(new AcceptInviteDto
        {
            UserId = "not-a-guid",
            Token = "whatever",
            Password = Password,
            DisplayName = string.Empty
        });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task An_unknown_account_and_a_bad_token_give_the_same_answer()
    {
        var mail = await InviteAsync();

        var unknown = await Identity.AcceptInviteAsync(new AcceptInviteDto
        {
            UserId = Guid.NewGuid().ToString(),
            Token = mail.Token,
            Password = Password,
            DisplayName = string.Empty
        });
        var badToken = await Identity.AcceptInviteAsync(new AcceptInviteDto
        {
            UserId = mail.UserId!.Value.ToString(),
            Token = "not-a-token",
            Password = Password,
            DisplayName = string.Empty
        });

        // The link is public, so telling them apart would say which account ids exist.
        Assert.Equal(unknown.ErrorMessage, badToken.ErrorMessage);
    }

    [Fact]
    public async Task An_invitation_password_below_the_minimum_length_is_a_validation_error()
    {
        var mail = await InviteAsync();

        var result = await Identity.AcceptInviteAsync(new AcceptInviteDto
        {
            UserId = mail.UserId!.Value.ToString(),
            Token = mail.Token,
            Password = "short",
            DisplayName = string.Empty
        });

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(AcceptInviteDto.Password), result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task An_invitation_password_the_policy_rejects_is_a_validation_error()
    {
        var mail = await InviteAsync();

        var result = await Identity.AcceptInviteAsync(new AcceptInviteDto
        {
            UserId = mail.UserId!.Value.ToString(),
            Token = mail.Token,
            // Eight characters, so the old [MinLength(8)] waved it through — and no lowercase letter,
            // which is the one password rule Identity is left enforcing.
            Password = "12345678",
            DisplayName = string.Empty
        });

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(AcceptInviteDto.Password), result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task An_invitation_whose_password_is_refused_activates_nothing()
    {
        var mail = await InviteAsync();

        await Identity.AcceptInviteAsync(new AcceptInviteDto
        {
            UserId = mail.UserId!.Value.ToString(),
            Token = mail.Token,
            Password = "12345678",
            DisplayName = string.Empty
        });

        // Confirming the address first and setting the password second left a confirmed, passwordless
        // account: activated as far as the admin screen was concerned, and resend-invite then refused
        // to help because it had "already accepted".
        var user = await ReloadAsync(mail.UserId.Value);
        Assert.False(user.EmailConfirmed);
        Assert.False(await UserManager.HasPasswordAsync(user));
    }

    [Fact]
    public async Task An_invitation_whose_password_is_refused_leaves_the_link_usable()
    {
        var mail = await InviteAsync();
        await Identity.AcceptInviteAsync(new AcceptInviteDto
        {
            UserId = mail.UserId!.Value.ToString(),
            Token = mail.Token,
            Password = "12345678",
            DisplayName = string.Empty
        });

        var result = await AcceptAsync(mail);

        // The invitee types a password the rules accept and the link they already have still works —
        // nothing about the first attempt was applied, the token included.
        Assert.True(result.IsSuccess, result.ErrorMessage);
        var user = await ReloadAsync(mail.UserId.Value);
        Assert.True(user.EmailConfirmed);
        Assert.True(await UserManager.CheckPasswordAsync(user, Password));
    }

    [Fact]
    public async Task Resending_an_invitation_mails_a_token_that_works()
    {
        var first = await InviteAsync();

        var resend = await Admin.ResendInviteAsync(first.UserId!.Value);

        Assert.True(resend.IsSuccess);
        var second = Email.Last!;
        Assert.NotEqual(first.Token, second.Token);
        Assert.True((await AcceptAsync(second)).IsSuccess);
    }

    [Fact]
    public async Task Resending_an_invitation_to_an_account_that_already_accepted_is_rejected()
    {
        var mail = await InviteAsync();
        await AcceptAsync(mail);

        var result = await Admin.ResendInviteAsync(mail.UserId!.Value);

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task Resending_an_invitation_for_an_unknown_account_is_not_found()
    {
        Assert.Equal(ResultCode.NotFound, (await Admin.ResendInviteAsync(Guid.NewGuid())).ResultCode);
    }

    [Fact]
    public async Task Resending_an_invitation_reports_a_failed_send()
    {
        var mail = await InviteAsync();
        Email.FailSends = true;

        var result = await Admin.ResendInviteAsync(mail.UserId!.Value);

        // Unlike the invite itself, sending the mail is the whole operation here.
        Assert.Equal(ResultCode.BadGateway, result.ResultCode);
    }

    private Task<OperationResult<Empty>> AcceptAsync(SentMail mail) =>
        Identity.AcceptInviteAsync(new AcceptInviteDto
        {
            UserId = mail.UserId!.Value.ToString(),
            Token = mail.Token,
            Password = Password,
            DisplayName = string.Empty
        });

    private async Task SetMustChangePasswordAsync(Guid userId)
    {
        var user = await UserManager.FindByIdAsync(userId.ToString());
        user!.MustChangePassword = true;
        await UserManager.UpdateAsync(user);
    }
}
