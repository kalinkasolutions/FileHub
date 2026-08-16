using System.ComponentModel.DataAnnotations;

namespace Dtos.Email;

public sealed class SendTestEmailDto
{
    /// <summary>Where the test message goes — usually the admin's own address.</summary>
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Recipient { get; set; }
}
