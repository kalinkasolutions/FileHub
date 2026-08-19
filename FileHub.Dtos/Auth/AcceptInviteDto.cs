using System.ComponentModel.DataAnnotations;

namespace Dtos.Auth;

/// <summary>
/// Payload behind the invitation link an admin-created account is activated with. There is no public
/// registration, so this is the only way an account gets its first password — and because the token
/// was mailed to the address on the account, redeeming it also proves the address.
/// </summary>
public sealed class AcceptInviteDto
{
    /// <summary>
    /// Mirrors <c>Password.RequireLowercase</c>, which Identity leaves on. <c>[\s\S]</c> rather than
    /// <c>.</c> so a password containing a newline is judged by the same rule Identity applies to it.
    /// </summary>
    private const string HasLowercase = @"^[\s\S]*\p{Ll}[\s\S]*$";

    [Required]
    public string UserId { get; set; }

    /// <summary>The email-confirmation token from the invitation mail.</summary>
    [Required]
    public string Token { get; set; }

    /// <summary>
    /// The password the account holder picks; an admin never learns it.
    /// <para>
    /// The rules here have to be the ones Identity enforces, not a subset of them. Redeeming the
    /// invitation confirms the address and sets the password, and a password this DTO waved through
    /// only for Identity to reject it left a confirmed account with no password — activated as far as
    /// the admin screen was concerned, and past the point where <c>resend-invite</c> would help.
    /// </para>
    /// </summary>
    [Required]
    [MinLength(8)]
    [RegularExpression(HasLowercase, ErrorMessage = "The password must contain a lowercase letter.")]
    public string Password { get; set; }

    /// <summary>
    /// Optional display name. Blank keeps whatever the admin typed when they created the account.
    /// </summary>
    [MaxLength(256)]
    public string DisplayName { get; set; }
}
