using Dal.Repositories.Email;
using Dtos.Email;
using Entities.Email;
using FileHub.BusinessLogic.Email;
using FileHub.BusinessLogic.Validation;
using Microsoft.Extensions.Logging;
using Shared;

namespace FileHub.BusinessLogic.Services.Email;

public sealed class EmailSettingService : IEmailSettingService
{
    private readonly IEmailSettingRepository m_repository;
    private readonly IEmailSettingsProvider m_settingsProvider;
    private readonly IEmailService m_emailService;
    private readonly ILogger<EmailSettingService> m_logger;

    public EmailSettingService(
        IEmailSettingRepository repository,
        IEmailSettingsProvider settingsProvider,
        IEmailService emailService,
        ILogger<EmailSettingService> logger
    )
    {
        m_repository = repository;
        m_settingsProvider = settingsProvider;
        m_emailService = emailService;
        m_logger = logger;
    }

    public async Task<OperationResult<EmailSettingDto>> GetAsync()
    {
        var setting = await m_settingsProvider.GetOrCreateAsync();
        return OperationResult<EmailSettingDto>.Success(ToDto(setting));
    }

    public async Task<OperationResult<EmailSettingDto>> UpdateAsync(UpdateEmailSettingDto dto)
    {
        var validation = DtoValidator.Validate(dto);
        if (validation.HasError)
        {
            return validation.MapError<EmailSettingDto>();
        }

        var setting = await m_settingsProvider.GetOrCreateAsync();

        var host = dto.SmtpHost.Trim();
        var username = dto.Username?.Trim() ?? string.Empty;

        // Where the stored secret would be sent, and how. A change to any of the three means the
        // password an admin never had to retype would go somewhere it was never given to — the
        // exfiltration this guard exists for is repointing the host and reading it off a listener.
        var destinationChanged = !string.Equals(host, setting.SmtpHost, StringComparison.OrdinalIgnoreCase)
                                 || dto.Port != setting.Port
                                 || !string.Equals(dto.SecureSocketOptions, setting.SecureSocketOptions, StringComparison.Ordinal);

        setting.SmtpHost = host;
        setting.Port = dto.Port;
        setting.Username = username;
        setting.FromAddress = dto.FromAddress.Trim();
        setting.FromName = dto.FromName?.Trim() ?? string.Empty;
        setting.SecureSocketOptions = dto.SecureSocketOptions;

        // "Keep the stored one" is worth having — the screen cannot read the secret back, so editing
        // a sender name must not mean retyping it — but the stored secret only keeps applying while
        // it still goes to the same place, and while there is a username to authenticate as. Without
        // one, EmailService does not authenticate at all and the password is dead weight the screen
        // would still report as present.
        var stillApplies = !destinationChanged && !string.IsNullOrEmpty(username);
        var passwordCleared = false;

        if (!string.IsNullOrEmpty(dto.Password))
        {
            setting.ProtectedPassword = m_settingsProvider.ProtectPassword(dto.Password);
        }
        else if (!stillApplies && !string.IsNullOrEmpty(setting.ProtectedPassword))
        {
            setting.ProtectedPassword = string.Empty;
            passwordCleared = true;
        }

        await m_repository.SaveChangesAsync();

        if (passwordCleared)
        {
            m_logger.LogWarning(
                "The stored SMTP password was cleared: host \"{SmtpHost}\" port {Port} transport {Transport} "
                + "user \"{Username}\" no longer match what it was saved for", setting.SmtpHost, setting.Port,
                setting.SecureSocketOptions, setting.Username);
        }

        m_logger.LogInformation("Email settings updated: host \"{SmtpHost}\" port {Port}", setting.SmtpHost, setting.Port);

        var result = ToDto(setting);
        result.PasswordCleared = passwordCleared;
        return OperationResult<EmailSettingDto>.Success(result);
    }

    public async Task<OperationResult<Empty>> SendTestAsync(SendTestEmailDto dto)
    {
        var validation = DtoValidator.Validate(dto);
        if (validation.HasError)
        {
            return validation;
        }

        return await m_emailService.SendTestMailAsync(dto.Recipient.Trim());
    }

    private static EmailSettingDto ToDto(EmailSetting setting) => new()
    {
        SmtpHost = setting.SmtpHost,
        Port = setting.Port,
        Username = setting.Username,
        FromAddress = setting.FromAddress,
        FromName = setting.FromName,
        SecureSocketOptions = setting.SecureSocketOptions,
        HasPassword = !string.IsNullOrEmpty(setting.ProtectedPassword)
    };
}
