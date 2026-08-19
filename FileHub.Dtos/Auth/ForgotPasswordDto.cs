using System.ComponentModel.DataAnnotations;

namespace Dtos.Auth;

public sealed class ForgotPasswordDto
{
    /// <summary>
    /// Capped at the longest address RFC 5321 allows: anonymous, so nothing else bounds what arrives
    /// here, and <c>[EmailAddress]</c> on its own accepts a megabyte-long local part.
    /// </summary>
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; }
}
