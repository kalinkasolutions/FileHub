using System.ComponentModel.DataAnnotations;

namespace Dtos.Account;

public sealed class ChangePasswordDto
{
    [Required]
    public string CurrentPassword { get; set; }

    [Required]
    [MinLength(8)]
    public string NewPassword { get; set; }

    /// <summary>Repeat of <see cref="NewPassword"/>; guards against typing a password you can't reproduce.</summary>
    [Required]
    [Compare(nameof(NewPassword), ErrorMessage = "The new passwords do not match.")]
    public string ConfirmPassword { get; set; }
}
