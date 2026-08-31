namespace Dal.Repositories.Identity;

/// <summary>
/// What a password sign-in attempt produced. Identity's own <c>SignInResult</c> stays in the data
/// layer; this is the shape the service decides on.
/// <para>
/// The distinction that matters: <see cref="LockedOut"/> and <see cref="NotAllowed"/> are decided
/// <em>before</em> the password is looked at, so on their own they say nothing about whether the
/// caller knows it. Only <see cref="Success"/>, <see cref="RequiresTwoFactor"/> and
/// <see cref="Failed"/> follow a hash comparison.
/// </para>
/// </summary>
public enum SignInOutcome
{
    /// <summary>Signed in; the cookie is written.</summary>
    Success,

    /// <summary>Password accepted, no cookie yet — the two-factor step has to finish the sign-in.</summary>
    RequiresTwoFactor,

    /// <summary>Too many failed attempts; the password was never checked.</summary>
    LockedOut,

    /// <summary>The account may not sign in at all (an unconfirmed address); the password was never checked.</summary>
    NotAllowed,

    /// <summary>The password did not match.</summary>
    Failed
}
