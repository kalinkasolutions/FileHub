using Dal.Repositories.Identity;
using Dtos.Auth;
using Entities.Account;
using FileHub.BusinessLogic.Email;
using FileHub.BusinessLogic.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared;

namespace FileHub.BusinessLogic.Services.Identity;

public sealed class IdentityService : IIdentityService
{
    /// <summary>
    /// One message for a malformed id, an unknown account and a rejected token alike: an invitation
    /// link is public, so telling them apart would say which ids exist.
    /// </summary>
    private const string BadInvite =
        "This invitation link is no longer valid. Ask an administrator to send you a new one.";

    private const string BadLink = "Invalid or expired link.";

    /// <summary>
    /// One message for every reset failure. Both halves are anonymous and this route carries no rate
    /// limit, so an unknown address answering differently from a rejected token is a free list of
    /// which addresses have accounts.
    /// </summary>
    private const string BadResetLink = "Invalid or expired password reset link.";

    /// <summary>Same reply for an unknown address and a wrong password, so neither can be probed.</summary>
    private const string BadCredentials = "Bad email or password.";

    private readonly ILogger<IdentityService> m_logger;
    private readonly IIdentityRepository m_identityRepository;

    /// <summary>
    /// The reset mail is sent outside the request, so <see cref="IEmailService"/> is resolved from a
    /// scope of its own rather than injected here — this one dies with the response.
    /// </summary>
    private readonly IServiceScopeFactory m_scopeFactory;

    public IdentityService(
        ILogger<IdentityService> logger,
        IIdentityRepository identityRepository,
        IServiceScopeFactory scopeFactory
    )
    {
        m_logger = logger;
        m_identityRepository = identityRepository;
        m_scopeFactory = scopeFactory;
    }

    public async Task<OperationResult<LoginResultDto>> LoginAsync(LoginDto loginDto)
    {
        var validation = DtoValidator.Validate(loginDto);
        if (validation.HasError)
        {
            return validation.MapError<LoginResultDto>();
        }

        // Sign-in resolves by email only; a username is a display name and intentionally not an identifier.
        var email = loginDto.Email.Trim();
        var user = await m_identityRepository.FindByEmailAsync(email);
        if (user is null)
        {
            // Not a shortcut: returning here without hashing anything is what made an unknown address
            // answer in ~2 ms where a known one took ~55 ms. The address itself is deliberately not
            // logged — it is anonymous, unbounded input and the log table has no retention.
            m_identityRepository.VerifyDummyPassword(loginDto.Password);
            m_logger.LogInformation("Failed login attempt for an address with no account");
            return OperationResult<LoginResultDto>.BadRequest(BadCredentials);
        }

        var outcome = await m_identityRepository.PasswordSignInAsync(user, loginDto.Password);

        if (outcome == SignInOutcome.Success)
        {
            m_logger.LogInformation("User {UserId} ({Username}) logged in", user.Id, user.UserName);
            return OperationResult<LoginResultDto>.Success(
                new LoginResultDto { MustChangePassword = user.MustChangePassword });
        }

        if (outcome == SignInOutcome.RequiresTwoFactor)
        {
            // The password was right but no cookie is issued yet: the user id is parked in the
            // two-factor cookie, and login-2fa finishes the sign-in from there.
            m_logger.LogInformation("User {UserId} passed the password step and needs a two-factor code", user.Id);
            return OperationResult<LoginResultDto>.Success(new LoginResultDto { RequiresTwoFactor = true });
        }

        if (outcome == SignInOutcome.Failed)
        {
            m_logger.LogInformation("Failed login attempt for user {UserId}", user.Id);
            return OperationResult<LoginResultDto>.BadRequest(BadCredentials);
        }

        // Locked out or not allowed: both are decided before the password is looked at, so on their
        // own they are true of any password at all — naming either would answer "does this address
        // have an account, and what state is it in" for a stranger typing gibberish. Check the
        // password now, which is also the hash comparison those branches skipped, so every failure
        // costs the same work.
        var passwordCorrect = await m_identityRepository.CheckPasswordAsync(user, loginDto.Password);
        if (!passwordCorrect)
        {
            // Nothing counted this attempt: the sign-in was refused before it reached the password.
            await m_identityRepository.AccessFailedAsync(user);
            m_logger.LogInformation("Failed login attempt for user {UserId}", user.Id);
            return OperationResult<LoginResultDto>.BadRequest(BadCredentials);
        }

        if (outcome == SignInOutcome.LockedOut)
        {
            m_logger.LogInformation("Login attempt for locked-out user {UserId}", user.Id);
            return OperationResult<LoginResultDto>.BadRequest("Too many failed attempts. Please try again later.");
        }

        // The caller has just proven they hold the password, so naming the reason tells them nothing
        // they could not find out by other means — and without it a user whose account was created but
        // never activated has no idea what to do next.
        m_logger.LogInformation("User {UserId} tried to log in with an unconfirmed email address", user.Id);
        return OperationResult<LoginResultDto>.BadRequest(
            "This account has not been activated yet. Follow the invitation link you were sent.");
    }

    public async Task<OperationResult<Empty>> AcceptInviteAsync(AcceptInviteDto acceptInviteDto)
    {
        var validation = DtoValidator.Validate(acceptInviteDto);
        if (validation.HasError)
        {
            return validation;
        }

        if (!Guid.TryParse(acceptInviteDto.UserId, out var userId))
        {
            return OperationResult<Empty>.BadRequest(BadInvite);
        }

        var user = await m_identityRepository.FindByIdAsync(userId);
        if (user is null)
        {
            return OperationResult<Empty>.BadRequest(BadInvite);
        }

        // The password goes through Identity's own rules before anything is written. The order below
        // cannot be swapped — the token is bound to the security stamp and setting a password rotates
        // it — so this is what keeps a rejected password from leaving a confirmed, passwordless
        // account behind: activated as far as the admin screen is concerned, and past the point where
        // resending the invitation would help.
        var passwordCheck = await m_identityRepository.ValidatePasswordAsync(user, acceptInviteDto.Password);
        if (passwordCheck.HasError)
        {
            // Reachable only if the DTO's rules and IdentityOptions have drifted apart, and only for
            // someone who already knows a user id — which is the same unguessable secret the
            // invitation link itself rests on.
            return passwordCheck;
        }

        var confirmResult = await m_identityRepository.ConfirmEmailAsync(user, acceptInviteDto.Token);
        if (confirmResult.HasError)
        {
            m_logger.LogInformation(
                "Invitation for user {UserId} was rejected: {Error}", user.Id, confirmResult.ErrorMessage);
            return OperationResult<Empty>.BadRequest(BadInvite);
        }

        var passwordResult = await m_identityRepository.SetFirstPasswordAsync(user, acceptInviteDto.Password);
        if (passwordResult.HasError)
        {
            // Pre-validation makes this all but unreachable, but half an activation is worse than
            // none: put the address back to unconfirmed so the account stays visibly invited and the
            // link — which confirming did not spend, only the password would have — still works.
            m_logger.LogWarning(
                "Invitation for user {UserId} could not set a password: {Error}", user.Id, passwordResult.ErrorMessage);
            await m_identityRepository.SetEmailUnconfirmedAsync(user);
            return passwordResult;
        }

        await SetInviteDisplayNameAsync(user, acceptInviteDto.DisplayName);

        var clearResult = await m_identityRepository.ClearMustChangePasswordAsync(user);
        if (clearResult.HasError)
        {
            return clearResult;
        }

        m_logger.LogInformation("User {UserId} accepted their invitation and set a password", user.Id);
        return OperationResult<Empty>.Success();
    }

    public async Task<OperationResult<Empty>> ConfirmEmailChangeAsync(ConfirmEmailChangeDto confirmEmailChangeDto)
    {
        var validation = DtoValidator.Validate(confirmEmailChangeDto);
        if (validation.HasError)
        {
            return validation;
        }

        if (!Guid.TryParse(confirmEmailChangeDto.UserId, out var userId))
        {
            return OperationResult<Empty>.BadRequest(BadLink);
        }

        var user = await m_identityRepository.FindByIdAsync(userId);
        if (user is null)
        {
            return OperationResult<Empty>.BadRequest(BadLink);
        }

        var email = confirmEmailChangeDto.Email.Trim();
        var result = await m_identityRepository.ChangeEmailAsync(user, email, confirmEmailChangeDto.Token);
        if (result.HasError)
        {
            // Anonymous, and the user id travels in the link, so Identity's own wording would answer
            // questions for whoever is holding it: "Invalid token." confirms the id belongs to an
            // account, and "Email 'x@y.com' is already taken." confirms an address does too. Same
            // reply as an unknown id, with the reason kept in the log.
            m_logger.LogInformation(
                "Email change for user {UserId} was rejected: {Error}", user.Id, result.ErrorMessage);
            return OperationResult<Empty>.BadRequest(BadLink);
        }

        // The token is bound to this address, so redeeming it also proves the address — the account
        // comes out confirmed even if it never was before.
        m_logger.LogInformation("User {UserId} changed their email address to {Email}", user.Id, email);
        return result;
    }

    public async Task<OperationResult<Empty>> SendPasswordResetAsync(ForgotPasswordDto forgotPasswordDto)
    {
        var validation = DtoValidator.Validate(forgotPasswordDto);
        if (validation.HasError)
        {
            return validation;
        }

        var email = forgotPasswordDto.Email.Trim();
        var user = await m_identityRepository.FindByEmailAsync(email);

        // Always report success so an attacker can't probe which emails have accounts. We only
        // generate a token and send the mail when the account actually exists.
        if (user is null)
        {
            m_logger.LogInformation("Password reset requested for an address with no account");
            return OperationResult<Empty>.Success();
        }

        var token = await m_identityRepository.GeneratePasswordResetTokenAsync(user);
        SendResetMailInBackground(user.Email!, user.Id, token);

        m_logger.LogInformation("Password reset requested for user {UserId}", user.Id);
        return OperationResult<Empty>.Success();
    }

    /// <summary>
    /// Hands the reset mail off and returns. Awaiting the send was what defeated the identical answer
    /// above: a known address took the SMTP round trip (~45 ms against a loopback fake, far more
    /// against a real relay) while an unknown one returned in ~2 ms, so the response time said what
    /// the response body would not.
    /// <para>
    /// A fresh scope because the request's one is disposed the moment we return, and a
    /// <c>try</c>/<c>catch</c> because nothing awaits this: an SMTP failure has to reach the log
    /// rather than becoming an unobserved task exception.
    /// </para>
    /// </summary>
    private void SendResetMailInBackground(string recipient, Guid userId, string token)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = m_scopeFactory.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                var mailResult = await emailService.SendResetPasswordMailAsync(recipient, token);
                if (mailResult.HasError)
                {
                    m_logger.LogWarning(
                        "Password reset email for user {UserId} could not be sent: {Error}",
                        userId, mailResult.ErrorMessage);
                }
            }
            catch (Exception exception)
            {
                m_logger.LogError(exception, "Password reset email for user {UserId} failed to send", userId);
            }
        });
    }

    public async Task<OperationResult<Empty>> ResetPasswordAsync(ResetPasswordDto resetPasswordDto)
    {
        var validation = DtoValidator.Validate(resetPasswordDto);
        if (validation.HasError)
        {
            return validation;
        }

        var user = await m_identityRepository.FindByEmailAsync(resetPasswordDto.Email.Trim());
        if (user is null)
        {
            return OperationResult<Empty>.BadRequest(BadResetLink);
        }

        var result = await m_identityRepository.ResetPasswordAsync(user, resetPasswordDto.Token, resetPasswordDto.Password);
        if (result.HasError)
        {
            // Identity's own "Invalid token." would come back only for an address that has an account,
            // so passing it through told an anonymous caller exactly which addresses do. The password
            // rules were already checked by the DTO, so nothing a legitimate user needs is lost here.
            m_logger.LogInformation("Password reset for user {UserId} was rejected: {Error}", user.Id, result.ErrorMessage);
            return OperationResult<Empty>.BadRequest(BadResetLink);
        }

        // The user just chose this password themselves, so whatever an admin set is no longer forced
        // on them — a reset is the other way out of the change-password gate.
        var clearResult = await m_identityRepository.ClearMustChangePasswordAsync(user);
        if (clearResult.HasError)
        {
            return clearResult;
        }

        m_logger.LogInformation("Reset password for user {UserId}", user.Id);
        return OperationResult<Empty>.Success();
    }

    /// <summary>
    /// Applies the display name the invitee typed, if any. A duplicate name is only logged: the
    /// account is already activated at this point, and a name is changeable from the account screen —
    /// failing the whole call would leave the user unable to retry with a link that is now spent.
    /// </summary>
    private async Task SetInviteDisplayNameAsync(FileHubUser user, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        var username = displayName.Trim();
        if (string.Equals(username, user.UserName, StringComparison.Ordinal))
        {
            return;
        }

        var result = await m_identityRepository.SetUsernameAsync(user, username);
        if (result.HasError)
        {
            m_logger.LogInformation(
                "User {UserId} could not take the display name \"{Username}\" while accepting their invitation: {Error}",
                user.Id, username, result.ErrorMessage);
        }
    }
}
