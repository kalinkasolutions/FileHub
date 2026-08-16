using System.ComponentModel.DataAnnotations;

namespace Dtos.Auth;

/// <summary>
/// Payload behind the invitation link an admin-created account is activated with. There is no public
/// registration, so this is the only way an account gets its first password — and because the token
/// was mailed to the address on the account, redeeming it also proves the address.
/// </summary>
public sealed class AcceptInviteDto
{
    [Required]
    public string UserId { get; set; }

    /// <summary>The email-confirmation token from the invitation mail.</summary>
    [Required]
    public string Token { get; set; }

    /// <summary>The password the account holder picks; an admin never learns it.</summary>
    [Required]
    [MinLength(8)]
    public string Password { get; set; }

    /// <summary>
    /// Optional display name. Blank keeps whatever the admin typed when they created the account.
    /// </summary>
    [MaxLength(256)]
    public string DisplayName { get; set; }
}
