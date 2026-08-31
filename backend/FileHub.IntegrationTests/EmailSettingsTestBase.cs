using Dal.Repositories.Email;
using FileHub.BusinessLogic.Email;
using FileHub.BusinessLogic.Services.Email;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;

namespace FileHub.IntegrationTests;

/// <summary>
/// Fixture for the runtime SMTP settings. The <c>Email</c> configuration section is the seed, not
/// the source of truth, so a fixture has to supply one — and the password goes through real Data
/// Protection, which <see cref="TestHostBase"/> already registers.
/// </summary>
public abstract class EmailSettingsTestBase : TestHostBase
{
    protected const string ConfiguredPassword = "smtp-secret";

    protected IEmailSettingService Settings => Services.GetRequiredService<IEmailSettingService>();
    protected IEmailSettingsProvider Provider => Services.GetRequiredService<IEmailSettingsProvider>();

    protected EmailSettingsTestBase(string configuredPassword) : base(services => Configure(services, configuredPassword))
    {
    }

    private static void Configure(IServiceCollection services, string configuredPassword)
    {
        services.Configure<EmailOptions>(options =>
        {
            options.SmtpHost = "smtp.example.com";
            options.Port = 587;
            options.Username = "postmaster";
            options.Password = configuredPassword;
            options.FromAddress = "filehub@example.com";
            options.FromName = "FileHub";
            options.SecureSocketOptions = SecureSocketOptions.StartTls;
        });

        services.AddScoped<IEmailSettingRepository, EmailSettingRepository>();
        services.AddScoped<IEmailSettingsProvider, EmailSettingsProvider>();
        services.AddScoped<IEmailSettingService, EmailSettingService>();
    }
}
