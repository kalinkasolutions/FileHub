using System.ComponentModel.DataAnnotations;

namespace Dtos.Account;

public sealed class UpdateUsernameDto
{
    /// <summary>The new display name. Still unique across accounts, so users stay distinguishable.</summary>
    [Required]
    [MaxLength(256)]
    public string Username { get; set; }
}
