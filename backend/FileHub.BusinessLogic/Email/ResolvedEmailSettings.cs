using MailKit.Security;

namespace FileHub.BusinessLogic.Email;

/// <summary>
/// The SMTP settings actually used for a send: the stored row with its password decrypted and its
/// transport name parsed. Built by <see cref="IEmailSettingsProvider"/> — nothing else decrypts.
/// </summary>
public sealed record ResolvedEmailSettings(
    string SmtpHost,
    int Port,
    string Username,
    string Password,
    string FromAddress,
    string FromName,
    SecureSocketOptions SecureSocketOptions)
{
    /// <summary>
    /// False while no host is set, which is the state a fresh install starts in. Callers check this
    /// instead of connecting to an empty host and waiting for the socket to fail.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(SmtpHost);
}
