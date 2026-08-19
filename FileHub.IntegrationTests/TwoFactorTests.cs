using Dtos.Account;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// Two-factor setup driven with a real generated code, so the authenticator secret, the code
/// verification and the recovery-code hashes all go through Identity rather than being asserted
/// around.
/// </summary>
public sealed class TwoFactorTests : AccountTestBase
{
    [Fact]
    public async Task Setup_returns_a_shared_key_and_an_otpauth_uri()
    {
        var ada = await CreateAccountAsync();

        var result = await StartTwoFactorSetupAsync(ada.Id);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value.SharedKey);
        Assert.StartsWith("otpauth://totp/FileHub%3Aada%40example.com?secret=", result.Value.AuthenticatorUri);
        Assert.Contains("issuer=FileHub", result.Value.AuthenticatorUri);
    }

    [Fact]
    public async Task Setup_does_not_turn_two_factor_on_by_itself()
    {
        var ada = await CreateAccountAsync();

        await StartTwoFactorSetupAsync(ada.Id);

        Assert.False((await ReloadAsync(ada.Id)).TwoFactorEnabled);
    }

    [Fact]
    public async Task Setup_reuses_the_secret_of_an_abandoned_attempt()
    {
        var ada = await CreateAccountAsync();

        var first = await StartTwoFactorSetupAsync(ada.Id);
        var second = await StartTwoFactorSetupAsync(ada.Id);

        // Reopening the screen must not invalidate a code the user already scanned.
        Assert.Equal(first.Value.SharedKey, second.Value.SharedKey);
    }

    [Fact]
    public async Task Enabling_with_a_generated_code_turns_two_factor_on_and_issues_recovery_codes()
    {
        var ada = await CreateAccountAsync();
        var setup = await StartTwoFactorSetupAsync(ada.Id);

        var result = await Account.EnableTwoFactorAsync(
            ada.Id, new EnableTwoFactorDto { Code = TotpCode.Current(setup.Value.SharedKey), CurrentPassword = Password });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(10, result.Value.Codes.Count);
        Assert.Equal(10, result.Value.Codes.Distinct().Count());
        Assert.True((await ReloadAsync(ada.Id)).TwoFactorEnabled);
    }

    [Fact]
    public async Task Enabling_accepts_a_code_typed_with_a_space()
    {
        var ada = await CreateAccountAsync();
        var setup = await StartTwoFactorSetupAsync(ada.Id);
        var code = TotpCode.Current(setup.Value.SharedKey);

        var result = await Account.EnableTwoFactorAsync(
            ada.Id, new EnableTwoFactorDto { Code = $"{code[..3]} {code[3..]}", CurrentPassword = Password });

        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    [Fact]
    public async Task Enabling_with_a_wrong_code_leaves_two_factor_off()
    {
        var ada = await CreateAccountAsync();
        await StartTwoFactorSetupAsync(ada.Id);

        var result = await Account.EnableTwoFactorAsync(ada.Id, new EnableTwoFactorDto { Code = "000000", CurrentPassword = Password });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.False((await ReloadAsync(ada.Id)).TwoFactorEnabled);
    }

    [Fact]
    public async Task Enabling_without_starting_the_setup_is_rejected()
    {
        var ada = await CreateAccountAsync();

        var result = await Account.EnableTwoFactorAsync(ada.Id, new EnableTwoFactorDto { Code = "123456", CurrentPassword = Password });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.False((await ReloadAsync(ada.Id)).TwoFactorEnabled);
    }

    [Fact]
    public async Task Setup_is_refused_while_two_factor_is_already_on()
    {
        var ada = await CreateAccountAsync();
        await EnableTwoFactorAsync(ada.Id);

        var result = await StartTwoFactorSetupAsync(ada.Id);

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task Recovery_codes_are_counted_on_the_account_screen()
    {
        var ada = await CreateAccountAsync();
        await EnableTwoFactorAsync(ada.Id);

        var account = await Account.GetAsync(ada.Id);

        Assert.True(account.Value.TwoFactorEnabled);
        Assert.Equal(10, account.Value.RecoveryCodesLeft);
    }

    [Fact]
    public async Task Regenerating_recovery_codes_replaces_the_old_set()
    {
        var ada = await CreateAccountAsync();
        var original = await EnableTwoFactorAsync(ada.Id);

        var result = await Account.RegenerateRecoveryCodesAsync(
            ada.Id, new RegenerateRecoveryCodesDto { CurrentPassword = Password });

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value.Codes.Count);
        Assert.Empty(result.Value.Codes.Intersect(original));
    }

    [Fact]
    public async Task Regenerating_recovery_codes_before_enabling_is_rejected()
    {
        var ada = await CreateAccountAsync();

        var result = await Account.RegenerateRecoveryCodesAsync(
            ada.Id, new RegenerateRecoveryCodesDto { CurrentPassword = Password });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    // ---- the password every one of these asks for ----

    [Fact]
    public async Task Setup_with_the_wrong_password_hands_out_no_secret()
    {
        var ada = await CreateAccountAsync();

        var result = await StartTwoFactorSetupAsync(ada.Id, "not-my-password");

        // A borrowed session cookie was enough to pair an authenticator, which decides who can sign in
        // from then on — turning 2FA off has asked for the password all along.
        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.Null(await UserManager.GetAuthenticatorKeyAsync(await ReloadAsync(ada.Id)));
    }

    [Fact]
    public async Task Enabling_with_the_wrong_password_leaves_two_factor_off()
    {
        var ada = await CreateAccountAsync();
        var setup = await StartTwoFactorSetupAsync(ada.Id);

        var result = await Account.EnableTwoFactorAsync(ada.Id, new EnableTwoFactorDto
        {
            Code = TotpCode.Current(setup.Value.SharedKey),
            CurrentPassword = "not-my-password"
        });

        // The authenticator code only proves a device was paired, not who paired it.
        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.False((await ReloadAsync(ada.Id)).TwoFactorEnabled);
    }

    [Fact]
    public async Task Regenerating_recovery_codes_with_the_wrong_password_keeps_the_old_set()
    {
        var ada = await CreateAccountAsync();
        var original = await EnableTwoFactorAsync(ada.Id);

        var result = await Account.RegenerateRecoveryCodesAsync(
            ada.Id, new RegenerateRecoveryCodesDto { CurrentPassword = "not-my-password" });

        // Recovery codes survive a password change and "sign out everywhere", so a set minted by
        // whoever borrowed the cookie would outlive every way the account holder has of taking the
        // session back.
        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.Equal(original.Count, (await Account.GetAsync(ada.Id)).Value.RecoveryCodesLeft);
    }

    [Fact]
    public async Task Enabling_without_a_password_is_a_validation_error()
    {
        var ada = await CreateAccountAsync();
        var setup = await StartTwoFactorSetupAsync(ada.Id);

        var result = await Account.EnableTwoFactorAsync(ada.Id, new EnableTwoFactorDto
        {
            Code = TotpCode.Current(setup.Value.SharedKey),
            CurrentPassword = string.Empty
        });

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(EnableTwoFactorDto.CurrentPassword), result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Disabling_turns_two_factor_off_and_discards_the_secret()
    {
        var ada = await CreateAccountAsync();
        var setup = await StartTwoFactorSetupAsync(ada.Id);
        var originalKey = setup.Value.SharedKey;
        await Account.EnableTwoFactorAsync(ada.Id, new EnableTwoFactorDto { Code = TotpCode.Current(originalKey), CurrentPassword = Password });

        var result = await Account.DisableTwoFactorAsync(ada.Id, new DisableTwoFactorDto { CurrentPassword = Password });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.False((await ReloadAsync(ada.Id)).TwoFactorEnabled);

        // A later setup pairs a fresh secret rather than accepting codes from an app entry the user
        // may have removed months ago.
        var newSetup = await StartTwoFactorSetupAsync(ada.Id);
        Assert.NotEqual(originalKey, newSetup.Value.SharedKey);
    }

    [Fact]
    public async Task Disabling_with_the_wrong_password_keeps_two_factor_on()
    {
        var ada = await CreateAccountAsync();
        await EnableTwoFactorAsync(ada.Id);

        var result = await Account.DisableTwoFactorAsync(
            ada.Id, new DisableTwoFactorDto { CurrentPassword = "not-my-password" });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.True((await ReloadAsync(ada.Id)).TwoFactorEnabled);
    }

    [Fact]
    public async Task Disabling_when_it_is_already_off_is_rejected()
    {
        var ada = await CreateAccountAsync();

        var result = await Account.DisableTwoFactorAsync(ada.Id, new DisableTwoFactorDto { CurrentPassword = Password });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }
}
