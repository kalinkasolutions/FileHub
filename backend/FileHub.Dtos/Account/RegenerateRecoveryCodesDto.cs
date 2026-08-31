using System.ComponentModel.DataAnnotations;

namespace Dtos.Account;

/// <summary>
/// Request for <c>POST api/account/2fa/recovery-codes</c>. Each code is a single-use way past the
/// second factor, so minting a fresh set is a credential issue and asks for the password.
/// </summary>
public sealed class RegenerateRecoveryCodesDto
{
    [Required]
    public string CurrentPassword { get; set; }
}
