using System.ComponentModel.DataAnnotations;

namespace Dtos.Admin;

/// <summary>
/// Edits an existing account. <see cref="Email"/> is carried so the form can round-trip the whole
/// user, but it must match the address the account already has: moving an account to a new address
/// has to be confirmed from that address, which only the user's own account screen can do.
/// </summary>
public sealed class UpdateUserDto
{
    [Required]
    [MaxLength(100)]
    public string Username { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    public string[] Roles { get; set; }
}
