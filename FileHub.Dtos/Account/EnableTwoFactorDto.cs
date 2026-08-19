using System.ComponentModel.DataAnnotations;

namespace Dtos.Account;

public sealed class EnableTwoFactorDto
{
    /// <summary>A current six-digit code from the authenticator app, proving the secret was stored.</summary>
    [Required]
    public string Code { get; set; }

    /// <summary>
    /// The account password. Pairing an authenticator is what decides who can sign in from now on, and
    /// it hands out recovery codes that survive both a password change and "sign out everywhere" — so
    /// it is at least as much of a credential change as turning the second factor off, which has asked
    /// for the password all along.
    /// </summary>
    [Required]
    public string CurrentPassword { get; set; }
}
