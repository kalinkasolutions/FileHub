using Dal.Repositories.Identity;
using Dtos.Auth;
using Entities.Account;
using FileHub.BusinessLogic.Email;
using FileHub.BusinessLogic.Validation;
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

    private readonly ILogger<IdentityService> m_logger;
    private readonly IIdentityRepository m_identityRepository;
    private readonly IEmailService m_emailService;

    public IdentityService(
        ILogger<IdentityService> logger,
        IIdentityRepository identityRepository,
        IEmailService emailService
    )
    {
        m_logger = logger;
        m_identityRepository = identityRepository;
        m_emailService = emailService;
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

        // The token has to be redeemed before anything else touches the account: it is bound to the
        // security stamp, and setting a password rotates that stamp.
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
        if (result.IsSuccess)
        {
            // The token is bound to this address, so redeeming it also proves the address — the account
            // comes out confirmed even if it never was before.
            m_logger.LogInformation("User {UserId} changed their email address to {Email}", user.Id, email);
        }

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
        m_logger.LogInformation("Password reset requested for {Email}", email);

        var user = await m_identityRepository.FindByEmailAsync(email);

        // Always report success so an attacker can't probe which emails have accounts. We only
        // generate a token and send the mail when the account actually exists.
        if (user is null)
        {
            m_logger.LogInformation("Password reset requested for {Email} but no account exists", email);
            return OperationResult<Empty>.Success();
        }

        var token = await m_identityRepository.GeneratePasswordResetTokenAsync(user);
        var mailResult = await m_emailService.SendResetPasswordMailAsync(user.Email!, token);
        if (mailResult.HasError)
        {
            m_logger.LogWarning("Password reset email could not be sent to {Email}", user.Email);
        }

        return OperationResult<Empty>.Success();
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
            // Don't reveal whether the email exists; a bad token yields the same message anyway.
            return OperationResult<Empty>.BadRequest("Invalid or expired password reset link.");
        }

        var result = await m_identityRepository.ResetPasswordAsync(user, resetPasswordDto.Token, resetPasswordDto.Password);
        if (result.HasError)
        {
            return result;
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
