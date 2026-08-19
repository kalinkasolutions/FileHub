using MailKit.Security;

namespace FileHub.BusinessLogic.Email;

/// <summary>
/// SMTP settings bound from the <c>Email</c> section of configuration
/// (appsettings.json / environment). These are only the <em>defaults</em>: on first read they seed
/// the <c>EmailSettings</c> row, and from then on the row an admin edits is what is used.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Address the mails are sent from (and shown as the sender).</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Display name for the sender; falls back to <see cref="FromAddress"/> when empty.</summary>
    public string FromName { get; set; } = string.Empty;

    public string SmtpHost { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>Bound by name from config, e.g. "StartTls", "SslOnConnect", "None", "Auto".</summary>
    public SecureSocketOptions SecureSocketOptions { get; set; } = SecureSocketOptions.Auto;
}
