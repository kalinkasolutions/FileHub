using System.Diagnostics;
using Dtos.Auth;
using Entities.Account;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// The password sign-in, which is anonymous, rate limited only per address and reachable from the
/// internet. Most of what is asserted here is what it must <em>not</em> say: an account's existence
/// and its state have to be invisible to anyone who does not already hold the password, in the reply
/// and in how long the reply takes.
/// </summary>
public sealed class LoginTests : IdentityTestBase
{
    private const string Wrong = "not-the-password";

    [Fact]
    public async Task The_right_password_signs_the_account_in()
    {
        UseHttpContext();
        var mail = await InviteAsync();
        await AcceptAsync(mail);

        var result = await Identity.LoginAsync(Login("ada@example.com", Password));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.False(result.Value.RequiresTwoFactor);
    }

    [Fact]
    public async Task An_account_still_behind_the_forced_change_says_so_once_it_is_in()
    {
        UseHttpContext();
        var ada = await CreateSignedInReadyUserAsync();
        ada.MustChangePassword = true;
        await UserManager.UpdateAsync(ada);

        var result = await Identity.LoginAsync(Login("ada@example.com", Password));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.True(result.Value.MustChangePassword);
    }

    [Fact]
    public async Task A_wrong_password_is_rejected()
    {
        await CreateSignedInReadyUserAsync();

        var result = await Identity.LoginAsync(Login("ada@example.com", Wrong));

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task An_unknown_address_and_a_wrong_password_give_the_same_answer()
    {
        await CreateSignedInReadyUserAsync();

        var unknown = await Identity.LoginAsync(Login("nobody@example.com", Wrong));
        var wrongPassword = await Identity.LoginAsync(Login("ada@example.com", Wrong));

        Assert.Equal(unknown.ResultCode, wrongPassword.ResultCode);
        Assert.Equal(unknown.ErrorMessage, wrongPassword.ErrorMessage);
    }

    [Fact]
    public async Task An_account_that_never_accepted_its_invitation_answers_a_stranger_the_same_way()
    {
        await InviteAsync();

        // The pending account used to answer "This account has not been activated yet" to any password
        // at all, because the sign-in decides that before it looks at one.
        var pending = await Identity.LoginAsync(Login("ada@example.com", Wrong));
        var unknown = await Identity.LoginAsync(Login("nobody@example.com", Wrong));

        Assert.Equal(ResultCode.BadRequest, pending.ResultCode);
        Assert.Equal(unknown.ErrorMessage, pending.ErrorMessage);
    }

    [Fact]
    public async Task A_locked_out_account_answers_a_stranger_the_same_way()
    {
        var ada = await CreateSignedInReadyUserAsync();
        await LockOutAsync(ada);

        // Same story: the lockout is checked before the password, so "Too many failed attempts" was an
        // answer anyone could get by guessing at an address they were only probing for.
        var locked = await Identity.LoginAsync(Login("ada@example.com", Wrong));
        var unknown = await Identity.LoginAsync(Login("nobody@example.com", Wrong));

        Assert.Equal(ResultCode.BadRequest, locked.ResultCode);
        Assert.Equal(unknown.ErrorMessage, locked.ErrorMessage);
    }

    [Fact]
    public async Task A_locked_out_account_says_so_to_whoever_knows_the_password()
    {
        var ada = await CreateSignedInReadyUserAsync();
        await LockOutAsync(ada);

        var result = await Identity.LoginAsync(Login("ada@example.com", Password));

        // Costs nothing to say: this caller already holds the credential the message would give away.
        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.Contains("Too many failed attempts", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unactivated_account_says_so_to_whoever_knows_the_password()
    {
        // An account an admin created with a password but that was never activated: unconfirmed, so
        // Identity refuses the sign-in, and the holder of the password is told why.
        await CreateUserAsync("ada@example.com", Password);
        var ada = await ReloadAsync((await UserManager.FindByEmailAsync("ada@example.com"))!.Id);
        ada.EmailConfirmed = false;
        await UserManager.UpdateAsync(ada);

        var result = await Identity.LoginAsync(Login("ada@example.com", Password));

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.Contains("has not been activated", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_wrong_password_at_a_refused_account_still_counts_towards_the_lockout()
    {
        var ada = await CreateSignedInReadyUserAsync();
        ada.EmailConfirmed = false;
        await UserManager.UpdateAsync(ada);

        await Identity.LoginAsync(Login("ada@example.com", Wrong));

        // The sign-in gives up before the password when the account may not sign in at all, so nothing
        // there counts the attempt — an unconfirmed address would otherwise be guessable for free.
        Assert.Equal(1, await UserManager.GetAccessFailedCountAsync(await ReloadAsync(ada.Id)));
    }

    [Fact]
    public async Task An_unknown_address_costs_the_same_hashing_as_a_known_one()
    {
        await CreateSignedInReadyUserAsync();

        // Warm the hasher and the query plans up; the first call of either pays for both.
        await Identity.LoginAsync(Login("ada@example.com", Wrong));
        await Identity.LoginAsync(Login("nobody@example.com", Wrong));

        var known = await MedianMillisecondsAsync("ada@example.com");
        var unknown = await MedianMillisecondsAsync("nobody@example.com");

        // Returning before hashing anything made this a ~25x gap, which says "no account here" as
        // plainly as a different message would. A third is a wide margin around "the same work".
        Assert.True(
            unknown > known / 3,
            $"An unknown address answered in {unknown:F1} ms against {known:F1} ms for a known one.");
    }

    [Fact]
    public async Task An_email_longer_than_an_address_can_be_is_a_validation_error()
    {
        // Anonymous, unbounded and logged: one request wrote a million-character row into a log table
        // that has no retention.
        var result = await Identity.LoginAsync(Login(new string('a', 1_000_000) + "@example.com", Wrong));

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(LoginDto.Email), result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task A_login_with_no_password_is_a_validation_error()
    {
        var result = await Identity.LoginAsync(new LoginDto { Email = "ada@example.com", Password = string.Empty });

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(LoginDto.Password), result.ValidationErrors.Keys);
    }

    private static LoginDto Login(string email, string password) => new()
    {
        Email = email,
        Password = password
    };

    /// <summary>An account that has accepted its invitation, so it can actually sign in.</summary>
    private async Task<FileHubUser> CreateSignedInReadyUserAsync()
    {
        var mail = await InviteAsync();
        var accepted = await AcceptAsync(mail);
        Assert.True(accepted.IsSuccess, accepted.ErrorMessage);
        return await ReloadAsync(mail.UserId!.Value);
    }

    private Task<OperationResult<Empty>> AcceptAsync(SentMail mail) =>
        Identity.AcceptInviteAsync(new AcceptInviteDto
        {
            UserId = mail.UserId!.Value.ToString(),
            Token = mail.Token,
            Password = Password,
            DisplayName = string.Empty
        });

    private async Task LockOutAsync(FileHubUser user)
    {
        var reloaded = await ReloadAsync(user.Id);
        await UserManager.SetLockoutEnabledAsync(reloaded, true);
        await UserManager.SetLockoutEndDateAsync(reloaded, DateTimeOffset.UtcNow.AddHours(1));
    }

    private async Task<double> MedianMillisecondsAsync(string email)
    {
        var samples = new List<double>();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var stopwatch = Stopwatch.StartNew();
            await Identity.LoginAsync(Login(email, Wrong));
            stopwatch.Stop();
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        samples.Sort();
        return samples[samples.Count / 2];
    }
}
