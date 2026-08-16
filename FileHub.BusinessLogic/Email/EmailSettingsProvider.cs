using System.Security.Cryptography;
using Dal.Repositories.Email;
using Entities.Email;
using MailKit.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileHub.BusinessLogic.Email;

public sealed class EmailSettingsProvider : IEmailSettingsProvider
{
    /// <summary>
    /// Data Protection purpose for the stored SMTP password. Changing it makes every stored
    /// password undecryptable, so it is a constant rather than something derived.
    /// </summary>
    private const string PasswordPurpose = "FileHub.EmailSettings.Password";

    /// <summary>
    /// Serialises the seeding insert. Reading the row and creating it when it is missing is two
    /// steps, and there is no unique constraint to catch two requests doing both at once — the first
    /// two calls on a fresh install (the admin screen loading while an invitation is being sent)
    /// would insert two rows, and half the settings an admin then edits would be the row nobody
    /// reads. Process-wide, which is enough for one container over one SQLite file.
    /// </summary>
    private static readonly SemaphoreSlim s_seedLock = new(1, 1);

    private readonly IEmailSettingRepository m_repository;
    private readonly EmailOptions m_options;
    private readonly IDataProtector m_protector;
    private readonly ILogger<EmailSettingsProvider> m_logger;

    public EmailSettingsProvider(
        IEmailSettingRepository repository,
        IOptions<EmailOptions> options,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<EmailSettingsProvider> logger
    )
    {
        m_repository = repository;
        m_options = options.Value;
        m_protector = dataProtectionProvider.CreateProtector(PasswordPurpose);
        m_logger = logger;
    }

    public async Task<EmailSetting> GetOrCreateAsync()
    {
        var setting = await m_repository.GetAsync();
        if (setting is not null)
        {
            return setting;
        }

        await s_seedLock.WaitAsync();

        try
        {
            return await CreateAsync();
        }
        finally
        {
            s_seedLock.Release();
        }
    }

    private async Task<EmailSetting> CreateAsync()
    {
        // Read again inside the lock: whoever held it may have been seeding the row this call was
        // about to insert a second copy of.
        var setting = await m_repository.GetAsync();
        if (setting is not null)
        {
            return setting;
        }

        // The config section is the seed, not the source of truth: an install configured purely by
        // environment sends mail without anyone opening the admin screen, and the first edit there
        // takes over from it.
        setting = new EmailSetting
        {
            SmtpHost = m_options.SmtpHost,
            Port = m_options.Port,
            Username = m_options.Username,
            ProtectedPassword = ProtectPassword(m_options.Password),
            FromAddress = m_options.FromAddress,
            FromName = m_options.FromName,
            SecureSocketOptions = m_options.SecureSocketOptions.ToString()
        };

        m_repository.Add(setting);
        await m_repository.SaveChangesAsync();
        m_logger.LogInformation("Seeded the email settings from configuration (host \"{SmtpHost}\")", setting.SmtpHost);
        return setting;
    }

    public async Task<ResolvedEmailSettings> GetAsync()
    {
        var setting = await GetOrCreateAsync();

        return new ResolvedEmailSettings(
            setting.SmtpHost ?? string.Empty,
            setting.Port,
            setting.Username ?? string.Empty,
            UnprotectPassword(setting.ProtectedPassword),
            setting.FromAddress ?? string.Empty,
            setting.FromName ?? string.Empty,
            ParseSecureSocketOptions(setting.SecureSocketOptions));
    }

    public string ProtectPassword(string password)
    {
        // Protecting an empty string would still produce a blob, which HasPassword would then read
        // as "a password is stored". Keep "no password" spelled as an empty column.
        if (string.IsNullOrEmpty(password))
        {
            return string.Empty;
        }

        return m_protector.Protect(password);
    }

    private string UnprotectPassword(string protectedPassword)
    {
        if (string.IsNullOrEmpty(protectedPassword))
        {
            return string.Empty;
        }

        try
        {
            return m_protector.Unprotect(protectedPassword);
        }
        catch (CryptographicException e)
        {
            // The key ring was lost or replaced (a wiped volume, a database restored next to fresh
            // keys). Failing the whole provider would take the admin screen down with it, so the
            // password reads as absent and the admin can save a new one.
            m_logger.LogError(e, "The stored SMTP password could not be decrypted; sending as if none were set");
            return string.Empty;
        }
    }

    // Stored by name so the column stays readable in the database; an unrecognised name falls back
    // to Auto, which is what MailKit would negotiate anyway.
    private SecureSocketOptions ParseSecureSocketOptions(string value)
    {
        if (Enum.TryParse<SecureSocketOptions>(value, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        m_logger.LogWarning("Unknown SecureSocketOptions \"{Value}\" in the email settings; using Auto", value);
        return SecureSocketOptions.Auto;
    }
}
