using System.ComponentModel.DataAnnotations;

namespace Dtos.Account;

/// <summary>
/// Request for <c>POST api/account/2fa/setup</c>. A POST rather than a GET because it asks for the
/// password: handing out the authenticator secret is the first half of pairing a device, so a
/// borrowed session cookie must not be enough to start it.
/// </summary>
public sealed class StartTwoFactorSetupDto
{
    [Required]
    public string CurrentPassword { get; set; }
}
