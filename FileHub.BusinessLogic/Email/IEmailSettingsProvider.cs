using Entities.Email;

namespace FileHub.BusinessLogic.Email;

/// <summary>
/// Owns the one place SMTP settings come from: the <see cref="EmailSetting"/> row, seeded from the
/// <c>Email</c> configuration section when it does not exist yet. It also owns the Data Protection
/// purpose the password is encrypted with, so no other class has to repeat that string.
/// </summary>
public interface IEmailSettingsProvider
{
    /// <summary>
    /// The row an admin edits, created from configuration on first read. The password on it is the
    /// encrypted one — use <see cref="GetAsync"/> to send with.
    /// </summary>
    Task<EmailSetting> GetOrCreateAsync();

    /// <summary>The effective settings, password decrypted and transport parsed.</summary>
    Task<ResolvedEmailSettings> GetAsync();

    /// <summary>Encrypts a password for storage on the row.</summary>
    string ProtectPassword(string password);
}
