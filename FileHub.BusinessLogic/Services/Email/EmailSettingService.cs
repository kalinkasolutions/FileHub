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
        setting.SmtpHost = dto.SmtpHost.Trim();
        setting.Port = dto.Port;
        setting.Username = dto.Username?.Trim() ?? string.Empty;
        setting.FromAddress = dto.FromAddress.Trim();
        setting.FromName = dto.FromName?.Trim() ?? string.Empty;
        setting.SecureSocketOptions = dto.SecureSocketOptions;

        // An empty password field means "keep the stored one": the screen cannot read the secret
        // back, so an admin editing the host must not have to retype it.
        if (!string.IsNullOrEmpty(dto.Password))
        {
            setting.ProtectedPassword = m_settingsProvider.ProtectPassword(dto.Password);
        }

        await m_repository.SaveChangesAsync();
        m_logger.LogInformation("Email settings updated: host \"{SmtpHost}\" port {Port}", setting.SmtpHost, setting.Port);
        return OperationResult<EmailSettingDto>.Success(ToDto(setting));
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
