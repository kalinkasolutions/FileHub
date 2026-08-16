using Entities.Account;
using Shared;

namespace Dal.Repositories.Identity;

/// <summary>
/// Data access for the flows that run while signed out: accepting an invitation, resetting a
/// password, confirming an email change. Wraps <c>UserManager</c> — no authorization or business
/// rules, those live in the service. There is no registration here: accounts are created by an admin.
/// </summary>
public interface IIdentityRepository
{
    Task<FileHubUser?> FindByEmailAsync(string email);
    Task<FileHubUser?> FindByIdAsync(Guid userId);

    /// <summary>Redeems the email-confirmation token the invitation mail carried.</summary>
    Task<OperationResult<Empty>> ConfirmEmailAsync(FileHubUser user, string token);

    /// <summary>
    /// Sets the password of an account that has none, which is the state an admin-created account is
    /// invited in. A re-invited account may still carry the password it was created with, so any
    /// existing one is dropped first — otherwise Identity refuses to add a second.
    /// </summary>
    Task<OperationResult<Empty>> SetFirstPasswordAsync(FileHubUser user, string password);

    Task<OperationResult<Empty>> SetUsernameAsync(FileHubUser user, string username);

    /// <summary>
    /// Clears the forced-password-change flag. The claim that gates the rest of the API is written
    /// when the sign-in cookie is issued, so this only has to be true by the time the user signs in.
    /// </summary>
    Task<OperationResult<Empty>> ClearMustChangePasswordAsync(FileHubUser user);

    /// <summary>
    /// Redeems a change-email token: moves the account to <paramref name="newEmail"/> and marks it
    /// confirmed. The token is generated in the account layer when the user requests the change.
    /// </summary>
    Task<OperationResult<Empty>> ChangeEmailAsync(FileHubUser user, string newEmail, string token);

    Task<string> GeneratePasswordResetTokenAsync(FileHubUser user);
    Task<OperationResult<Empty>> ResetPasswordAsync(FileHubUser user, string token, string newPassword);
}
