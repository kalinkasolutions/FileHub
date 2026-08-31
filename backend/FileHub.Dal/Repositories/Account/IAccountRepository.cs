using Entities.Account;
using Shared;

namespace Dal.Repositories.Account;

/// <summary>
/// Data access for the signed-in user's own account: profile fields, credentials and the
/// authenticator secret. Wraps <c>UserManager</c> the way <see cref="Identity.IIdentityRepository"/>
/// does for the anonymous auth flows — no authorization or business rules, those live in the service.
/// There is no delete here: accounts are removed by an admin, not by their holder.
/// </summary>
public interface IAccountRepository
{
    Task<FileHubUser?> FindByIdAsync(Guid userId);
    Task<FileHubUser?> FindByEmailAsync(string email);

    /// <summary>Whether <paramref name="password"/> is the user's current password.</summary>
    Task<bool> CheckPasswordAsync(FileHubUser user, string password);

    Task<OperationResult<Empty>> ChangePasswordAsync(FileHubUser user, string currentPassword, string newPassword);
    Task<OperationResult<Empty>> SetUsernameAsync(FileHubUser user, string username);

    /// <summary>Clears the forced-password-change flag, which is what a password change earns.</summary>
    Task<OperationResult<Empty>> ClearMustChangePasswordAsync(FileHubUser user);

    /// <summary>Token for the <em>new</em> address, to be mailed there and redeemed by the auth layer.</summary>
    Task<string> GenerateChangeEmailTokenAsync(FileHubUser user, string newEmail);

    /// <summary>Rotates the security stamp, which invalidates every issued auth cookie for this user.</summary>
    Task UpdateSecurityStampAsync(FileHubUser user);

    /// <summary>The stored authenticator secret, or null/empty when the user never set one up.</summary>
    Task<string?> GetAuthenticatorKeyAsync(FileHubUser user);

    /// <summary>Generates a fresh authenticator secret (discarding any previous one) and returns it.</summary>
    Task<string> ResetAuthenticatorKeyAsync(FileHubUser user);

    /// <summary>Whether <paramref name="code"/> is currently valid for the user's authenticator secret.</summary>
    Task<bool> VerifyAuthenticatorCodeAsync(FileHubUser user, string code);

    Task<OperationResult<Empty>> SetTwoFactorEnabledAsync(FileHubUser user, bool enabled);

    /// <summary>Replaces the user's recovery codes and returns the new plaintext set (only ever available here).</summary>
    Task<List<string>> GenerateRecoveryCodesAsync(FileHubUser user, int count);

    Task<int> CountRecoveryCodesAsync(FileHubUser user);
}
