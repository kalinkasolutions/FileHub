using Dtos.Email;
using Shared;

namespace FileHub.BusinessLogic.Services.Email;

/// <summary>The admin surface over the SMTP settings, behind <c>api/admin/email</c>.</summary>
public interface IEmailSettingService
{
    /// <summary>The stored settings, without the password.</summary>
    Task<OperationResult<EmailSettingDto>> GetAsync();

    /// <summary>Writes the settings; an empty password field keeps the stored one.</summary>
    Task<OperationResult<EmailSettingDto>> UpdateAsync(UpdateEmailSettingDto dto);

    /// <summary>Sends a test message, so an admin can check what they just saved actually sends.</summary>
    Task<OperationResult<Empty>> SendTestAsync(SendTestEmailDto dto);
}
