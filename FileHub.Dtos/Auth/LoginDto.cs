using System.ComponentModel.DataAnnotations;

namespace Dtos.Auth;

public sealed class LoginDto
{
    /// <summary>
    /// Email address of the account to sign in to; the username is a display name only.
    /// <para>
    /// Capped at the longest address RFC 5321 allows. This is an anonymous endpoint whose failures are
    /// logged, and the log table has no retention — uncapped, one request wrote a million-character
    /// row into it.
    /// </para>
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string Email { get; set; }

    [Required]
    public string Password { get; set; }
}
