using System.ComponentModel.DataAnnotations;

namespace Dtos.Auth;

public sealed class ResetPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    public string Token { get; set; }

    [Required]
    [MinLength(8)]
    public string Password { get; set; }
}
