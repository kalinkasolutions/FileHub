using Dtos.Auth;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// The forgot-password round trip. It is the other way out of the forced-password-change gate, so
/// it has to clear that flag as well as set the password.
/// </summary>
public sealed class PasswordResetTests : IdentityTestBase
{
    private const string NewPassword = "brand-new-password";

    [Fact]
    public async Task Requesting_a_reset_mails_a_token_to_a_known_address()
    {
        await CreateUserAsync("ada@example.com");

        var result = await Identity.SendPasswordResetAsync(new ForgotPasswordDto { Email = "ada@example.com" });

        Assert.True(result.IsSuccess);
        Assert.Equal(MailKind.ResetPassword, Email.Last!.Kind);
        Assert.Equal("ada@example.com", Email.Last.Recipient);
        Assert.False(string.IsNullOrEmpty(Email.Last.Token));
    }

    [Fact]
    public async Task Requesting_a_reset_for_an_unknown_address_reports_success_without_sending()
    {
        var result = await Identity.SendPasswordResetAsync(new ForgotPasswordDto { Email = "nobody@example.com" });

        // Reporting success regardless is what stops the route being used to probe for accounts.
        Assert.True(result.IsSuccess);
        Assert.Empty(Email.Sent);
    }

    [Fact]
    public async Task Requesting_a_reset_still_reports_success_when_the_mail_cannot_be_sent()
    {
        await CreateUserAsync("ada@example.com");
        Email.FailSends = true;

        var result = await Identity.SendPasswordResetAsync(new ForgotPasswordDto { Email = "ada@example.com" });

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Requesting_a_reset_with_a_malformed_address_is_a_validation_error()
    {
        var result = await Identity.SendPasswordResetAsync(new ForgotPasswordDto { Email = "not-an-address" });

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(ForgotPasswordDto.Email), result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Resetting_with_the_mailed_token_sets_the_new_password()
    {
        var ada = await CreateUserAsync("ada@example.com");
        var token = await RequestResetAsync("ada@example.com");

        var result = await Identity.ResetPasswordAsync(new ResetPasswordDto
        {
            Email = "ada@example.com",
            Token = token,
            Password = NewPassword
        });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.True(await UserManager.CheckPasswordAsync(await ReloadAsync(ada.Id), NewPassword));
    }

    [Fact]
    public async Task Resetting_clears_the_forced_password_change()
    {
        var ada = await CreateUserAsync("ada@example.com");
        ada.MustChangePassword = true;
        await UserManager.UpdateAsync(ada);
        var token = await RequestResetAsync("ada@example.com");

        await Identity.ResetPasswordAsync(new ResetPasswordDto
        {
            Email = "ada@example.com",
            Token = token,
            Password = NewPassword
        });

        // The user just chose this password themselves, so whatever an admin set is no longer
        // forced on them.
        Assert.False((await ReloadAsync(ada.Id)).MustChangePassword);
    }

    [Fact]
    public async Task A_reset_token_cannot_be_used_twice()
    {
        await CreateUserAsync("ada@example.com");
        var token = await RequestResetAsync("ada@example.com");
        await Identity.ResetPasswordAsync(new ResetPasswordDto
        {
            Email = "ada@example.com",
            Token = token,
            Password = NewPassword
        });

        var result = await Identity.ResetPasswordAsync(new ResetPasswordDto
        {
            Email = "ada@example.com",
            Token = token,
            Password = "third-password"
        });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task Resetting_with_a_garbage_token_is_rejected()
    {
        var ada = await CreateUserAsync("ada@example.com");

        var result = await Identity.ResetPasswordAsync(new ResetPasswordDto
        {
            Email = "ada@example.com",
            Token = "not-a-token",
            Password = NewPassword
        });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.False(await UserManager.CheckPasswordAsync(await ReloadAsync(ada.Id), NewPassword));
    }

    [Fact]
    public async Task Resetting_with_a_token_issued_for_another_account_is_rejected()
    {
        await CreateUserAsync("ada@example.com");
        await CreateUserAsync("grace@example.com");
        var gracesToken = await RequestResetAsync("grace@example.com");

        var result = await Identity.ResetPasswordAsync(new ResetPasswordDto
        {
            Email = "ada@example.com",
            Token = gracesToken,
            Password = NewPassword
        });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task Resetting_hides_whether_the_address_has_an_account()
    {
        var result = await Identity.ResetPasswordAsync(new ResetPasswordDto
        {
            Email = "nobody@example.com",
            Token = "whatever",
            Password = NewPassword
        });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task Resetting_to_a_password_below_the_minimum_length_is_a_validation_error()
    {
        await CreateUserAsync("ada@example.com");
        var token = await RequestResetAsync("ada@example.com");

        var result = await Identity.ResetPasswordAsync(new ResetPasswordDto
        {
            Email = "ada@example.com",
            Token = token,
            Password = "short"
        });

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(ResetPasswordDto.Password), result.ValidationErrors.Keys);
    }

    private async Task<string> RequestResetAsync(string email)
    {
        var result = await Identity.SendPasswordResetAsync(new ForgotPasswordDto { Email = email });
        Assert.True(result.IsSuccess);
        return Email.Last!.Token;
    }
}
