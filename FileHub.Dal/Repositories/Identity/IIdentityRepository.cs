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

    /// <summary>
    /// Signs the user in with a persistent cookie, counting a wrong password towards the lockout.
    /// Issues the cookie itself on success, or parks the user id for the two-factor step.
    /// </summary>
    Task<SignInOutcome> PasswordSignInAsync(FileHubUser user, string password);

    /// <summary>Whether <paramref name="password"/> is the user's current password. Does not sign anyone in.</summary>
    Task<bool> CheckPasswordAsync(FileHubUser user, string password);

    /// <summary>
    /// Counts a failed attempt towards the lockout. Needed for the attempts <c>PasswordSignInAsync</c>
    /// rejects before it reaches the password, which it therefore never counts.
    /// </summary>
    Task AccessFailedAsync(FileHubUser user);

    /// <summary>
    /// Verifies <paramref name="password"/> against a throwaway hash and discards the answer. Called
    /// when the address has no account, so that reply costs the same hashing work as a real failed
    /// sign-in: returning early instead made an unknown address answer in ~2 ms against ~55 ms for a
    /// known one, which is the same disclosure as a different error message.
    /// </summary>
    void VerifyDummyPassword(string password);

    /// <summary>
    /// Runs Identity's own password rules without writing anything, so a password it would reject can
    /// be refused before any part of a multi-step flow has been applied.
    /// </summary>
    Task<OperationResult<Empty>> ValidatePasswordAsync(FileHubUser user, string password);

    /// <summary>Redeems the email-confirmation token the invitation mail carried.</summary>
    Task<OperationResult<Empty>> ConfirmEmailAsync(FileHubUser user, string token);

    /// <summary>
    /// Puts the address back to unconfirmed. Undoes a redeemed invitation whose password step then
    /// failed — confirming does not rotate the security stamp, so the link the invitee holds still
    /// works afterwards.
    /// </summary>
    Task<OperationResult<Empty>> SetEmailUnconfirmedAsync(FileHubUser user);

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
