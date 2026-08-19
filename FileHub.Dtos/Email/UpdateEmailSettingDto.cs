using System.ComponentModel.DataAnnotations;

namespace Dtos.Email;

public sealed class UpdateEmailSettingDto
{
    [Required]
    [MaxLength(255)]
    public string SmtpHost { get; set; }

    [Range(1, 65535)]
    public int Port { get; set; }

    [MaxLength(255)]
    public string Username { get; set; }

    /// <summary>
    /// Left empty to keep the stored password — the admin screen cannot read it back, so requiring
    /// it here would mean retyping the secret to change a sender name. It is <em>not</em> kept when
    /// the save changes where the password would be sent (host, port or transport) or removes the
    /// username: the response then comes back with <c>PasswordCleared</c> set.
    /// </summary>
    [MaxLength(255)]
    public string Password { get; set; }

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string FromAddress { get; set; }

    [MaxLength(255)]
    public string FromName { get; set; }

    /// <summary>
    /// MailKit's <c>SecureSocketOptions</c> by name. Checked here rather than parsed leniently, so a
    /// typo is a 400 on the screen that made it instead of a silent fall back to Auto on the next send.
    /// </summary>
    [Required]
    [AllowedValues("None", "Auto", "SslOnConnect", "StartTls", "StartTlsWhenAvailable")]
    public string SecureSocketOptions { get; set; }
}
