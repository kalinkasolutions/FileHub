using System.ComponentModel.DataAnnotations;

namespace Dtos.Admin;

/// <summary>
/// What an admin fills in to create an account. There is no password field on purpose: the
/// invitation link is what sets the first password, so an admin never learns one.
/// </summary>
public sealed class InviteUserDto
{
    [Required]
    [MaxLength(100)]
    public string Username { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; }

    /// <summary>
    /// Role names from <c>Shared.Roles</c>. <c>User</c> is added whether or not it is listed —
    /// an account without it could sign in and see nothing.
    /// </summary>
    [Required]
    public string[] Roles { get; set; }
}
