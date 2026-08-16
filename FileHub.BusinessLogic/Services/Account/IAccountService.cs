using Dtos.Account;
using Shared;

namespace FileHub.BusinessLogic.Services.Account;

/// <summary>
/// Self-service for the signed-in user's own account. Every method takes the caller's id from the
/// endpoint, so there is no cross-user surface here: a user can only ever read or change themselves.
/// Deleting an account is not here — that is an admin action.
/// </summary>
public interface IAccountService
{
    Task<OperationResult<AccountDto>> GetAsync(Guid userId);

    /// <summary>Changes the display name shown next to the shares this user created.</summary>
    Task<OperationResult<AccountDto>> UpdateUsernameAsync(Guid userId, UpdateUsernameDto dto);

    /// <summary>
    /// Changes the password and clears the forced-change flag, which is the only way out of the
    /// gate a freshly invited (or admin-reset) account starts behind.
    /// </summary>
    Task<OperationResult<Empty>> ChangePasswordAsync(Guid userId, ChangePasswordDto dto);

    /// <summary>
    /// Starts an email change by mailing a confirmation link to the new address. The account keeps its
    /// current address until that link is followed (<c>IIdentityService.ConfirmEmailChangeAsync</c>).
    /// </summary>
    Task<OperationResult<Empty>> RequestEmailChangeAsync(Guid userId, ChangeEmailDto dto);

    /// <summary>Invalidates every auth cookie issued to this user, on all their devices.</summary>
    Task<OperationResult<Empty>> SignOutEverywhereAsync(Guid userId);

    /// <summary>
    /// The authenticator secret to scan or type, for a user who does not have 2FA on yet.
    /// <para>
    /// All four two-factor operations take the account password, not just the one that turns it off.
    /// Every one of them changes what it takes to sign in as this account, and a session cookie is a
    /// weaker thing to hold than the password — someone with a borrowed one could otherwise pair
    /// their own authenticator and walk away with recovery codes that survive a password change.
    /// </para>
    /// </summary>
    Task<OperationResult<TwoFactorSetupDto>> StartTwoFactorSetupAsync(Guid userId, StartTwoFactorSetupDto dto);

    /// <summary>Verifies a code against the pending secret and, if it matches, turns 2FA on and issues recovery codes.</summary>
    Task<OperationResult<RecoveryCodesDto>> EnableTwoFactorAsync(Guid userId, EnableTwoFactorDto dto);

    /// <summary>Turns 2FA off and discards the authenticator secret, so re-enabling starts from a new one.</summary>
    Task<OperationResult<Empty>> DisableTwoFactorAsync(Guid userId, DisableTwoFactorDto dto);

    /// <summary>Replaces the remaining recovery codes with a fresh set.</summary>
    Task<OperationResult<RecoveryCodesDto>> RegenerateRecoveryCodesAsync(Guid userId, RegenerateRecoveryCodesDto dto);
}
