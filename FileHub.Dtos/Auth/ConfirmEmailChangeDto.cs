using System.ComponentModel.DataAnnotations;

namespace Dtos.Auth;

/// <summary>
/// Payload behind the link mailed to a user's <em>new</em> address when they request an email change.
/// The link is followed while signed out just as often as signed in, so it carries the user id.
/// </summary>
public sealed class ConfirmEmailChangeDto
{
    [Required]
    public string UserId { get; set; }

    /// <summary>The new address the token was issued for.</summary>
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; }

    [Required]
    public string Token { get; set; }
}
