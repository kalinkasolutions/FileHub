using System.ComponentModel.DataAnnotations;

namespace Dtos.Auth;

public sealed class ResetPasswordDto
{
    /// <summary>
    /// Mirrors <c>Password.RequireLowercase</c>, which Identity leaves on. The rules have to be
    /// declared here rather than left to Identity: everything the reset itself rejects comes back as
    /// one deliberately uninformative message (a differing one would say which addresses have
    /// accounts), so a password the policy refuses has to be caught before that, where the caller
    /// can still be told why.
    /// </summary>
    private const string HasLowercase = @"^[\s\S]*\p{Ll}[\s\S]*$";

    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; }

    [Required]
    public string Token { get; set; }

    [Required]
    [MinLength(8)]
    [RegularExpression(HasLowercase, ErrorMessage = "The password must contain a lowercase letter.")]
    public string Password { get; set; }
}
