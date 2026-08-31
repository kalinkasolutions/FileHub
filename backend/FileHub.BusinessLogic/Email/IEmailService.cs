using Shared;

namespace FileHub.BusinessLogic.Email;

public interface IEmailService
{
    /// <summary>
    /// Sends the invitation an admin-created account is activated with: one link that both sets the
    /// first password and confirms the address, so an admin never learns a user's password.
    /// </summary>
    Task<OperationResult<Empty>> SendInviteMailAsync(string recipient, Guid userId, string token);

    /// <summary>Sends the password-reset link for a user who requested it.</summary>
    Task<OperationResult<Empty>> SendResetPasswordMailAsync(string recipient, string token);

    /// <summary>
    /// Sends the confirmation link for an email change to the <em>new</em> address, so the account
    /// only moves once the user proves they can read mail there.
    /// </summary>
    Task<OperationResult<Empty>> SendChangeEmailMailAsync(string newEmail, Guid userId, string token);

    /// <summary>Sends a fixed message, so an admin can check the SMTP settings they just saved.</summary>
    Task<OperationResult<Empty>> SendTestMailAsync(string recipient);
}
