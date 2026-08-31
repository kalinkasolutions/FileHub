using Dtos.Auth;
using Shared;

namespace FileHub.BusinessLogic.Services.Identity;

/// <summary>
/// The flows that run while signed out. There is no registration: an admin creates the account and
/// the holder activates it through <see cref="AcceptInviteAsync"/>.
/// </summary>
public interface IIdentityService
{
    /// <summary>
    /// Signs in with an email address and a password, writing the auth cookie — or, when the account
    /// carries a second factor, parking the sign-in for <c>api/auth/login-2fa</c> to finish.
    /// <para>
    /// Anonymous and on the internet, so every failure a stranger can provoke has to look and cost the
    /// same: one message, and one password hash comparison whether or not the address exists.
    /// </para>
    /// </summary>
    Task<OperationResult<LoginResultDto>> LoginAsync(LoginDto loginDto);

    /// <summary>
    /// Activates an admin-created account from its invitation link: redeems the email-confirmation
    /// token, sets the first password and clears the forced-change flag. One call, because the token
    /// was mailed to the address on the account — using it proves the address as well.
    /// </summary>
    Task<OperationResult<Empty>> AcceptInviteAsync(AcceptInviteDto acceptInviteDto);

    /// <summary>
    /// Completes an email change started from the account screen. Anonymous on purpose: the link is
    /// often opened in the mail app's browser, where the user isn't signed in.
    /// </summary>
    Task<OperationResult<Empty>> ConfirmEmailChangeAsync(ConfirmEmailChangeDto confirmEmailChangeDto);

    Task<OperationResult<Empty>> SendPasswordResetAsync(ForgotPasswordDto forgotPasswordDto);
    Task<OperationResult<Empty>> ResetPasswordAsync(ResetPasswordDto resetPasswordDto);
}
