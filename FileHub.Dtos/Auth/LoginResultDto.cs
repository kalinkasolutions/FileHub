namespace Dtos.Auth;

/// <summary>
/// Outcome of a successful password check. When <see cref="RequiresTwoFactor"/> is true the caller is
/// not signed in yet and must post the authenticator code to <c>api/auth/login-2fa</c> to finish.
/// </summary>
public sealed class LoginResultDto
{
    public bool RequiresTwoFactor { get; set; }

    /// <summary>
    /// The account was activated with a password an admin chose, so the session may do nothing but
    /// change it. The SPA sends the user straight to the password screen rather than the file browser.
    /// </summary>
    public bool MustChangePassword { get; set; }
}
