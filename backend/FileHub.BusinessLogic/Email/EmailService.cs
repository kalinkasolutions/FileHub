using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Shared;

namespace FileHub.BusinessLogic.Email;

/// <summary>
/// Sends transactional mail via SMTP (MailKit). Bodies are HTML templates loaded from the
/// <c>EmailTemplates</c> folder next to the running app, with <c>@Placeholder</c> tokens replaced.
/// The SMTP settings come from <see cref="IEmailSettingsProvider"/> rather than configuration, so
/// an admin can change them without a redeploy.
/// </summary>
public sealed class EmailService : IEmailService
{
    private readonly IEmailSettingsProvider m_settingsProvider;
    private readonly AppOptions m_appOptions;
    private readonly ILogger<EmailService> m_logger;

    public EmailService(
        IEmailSettingsProvider settingsProvider,
        IOptions<AppOptions> appOptions,
        ILogger<EmailService> logger
    )
    {
        m_settingsProvider = settingsProvider;
        m_appOptions = appOptions.Value;
        m_logger = logger;
    }

    public Task<OperationResult<Empty>> SendInviteMailAsync(string recipient, Guid userId, string token)
    {
        var inviteUrl = $"{m_appOptions.TrimmedBaseUrl()}/accept-invite" +
                        $"?userId={Uri.EscapeDataString(userId.ToString())}" +
                        $"&token={Uri.EscapeDataString(token)}";

        return SendTemplatedAsync(
            recipient,
            "Your FileHub account is ready",
            "InviteTemplate.html",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Title"] = "Activate your account",
                ["InviteUrl"] = inviteUrl
            });
    }

    public Task<OperationResult<Empty>> SendResetPasswordMailAsync(string recipient, string token)
    {
        var resetUrl = $"{m_appOptions.TrimmedBaseUrl()}/reset-password" +
                       $"?email={Uri.EscapeDataString(recipient)}" +
                       $"&token={Uri.EscapeDataString(token)}";

        return SendTemplatedAsync(
            recipient,
            "Reset your FileHub password",
            "ResetPasswordEmailTemplate.html",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Title"] = "Reset your password",
                ["ResetUrl"] = resetUrl
            });
    }

    public Task<OperationResult<Empty>> SendChangeEmailMailAsync(string newEmail, Guid userId, string token)
    {
        var confirmUrl = $"{m_appOptions.TrimmedBaseUrl()}/confirm-email-change" +
                         $"?userId={Uri.EscapeDataString(userId.ToString())}" +
                         $"&email={Uri.EscapeDataString(newEmail)}" +
                         $"&token={Uri.EscapeDataString(token)}";

        return SendTemplatedAsync(
            newEmail,
            "Confirm your new FileHub email address",
            "ChangeEmailTemplate.html",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Title"] = "Confirm your new email address",
                ["ConfirmUrl"] = confirmUrl
            });
    }

    public Task<OperationResult<Empty>> SendTestMailAsync(string recipient)
    {
        return SendTemplatedAsync(
            recipient,
            "FileHub test email",
            "TestEmailTemplate.html",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Title"] = "Your SMTP settings work",
                ["AppUrl"] = m_appOptions.TrimmedBaseUrl()
            });
    }

    private async Task<OperationResult<Empty>> SendTemplatedAsync(
        string recipient,
        string subject,
        string templateName,
        Dictionary<string, string> templateData
    )
    {
        // Parsed before anything else is done, and with TryParse rather than Parse: [EmailAddress]
        // on the DTOs accepts addresses MimeKit refuses ("bad user@example.com"), and a
        // ParseException here used to escape as a 500 — including out of the anonymous
        // forgot-password route, whose whole contract is that it always succeeds.
        if (!MailboxAddress.TryParse(recipient, out var to))
        {
            m_logger.LogWarning("Cannot send \"{Subject:l}\": {Recipient:l} is not a usable address", subject, recipient);
            return OperationResult<Empty>.BadRequest($"\"{recipient}\" is not an address email can be sent to.");
        }

        var settings = await m_settingsProvider.GetAsync();
        if (!settings.IsConfigured)
        {
            // Connecting to an empty host only buys a socket error a minute later, and the admin
            // needs to be told what is actually missing.
            m_logger.LogError("Cannot send \"{Subject:l}\": no SMTP host is configured", subject);
            return OperationResult<Empty>.BadGateway(
                "Email is not configured: no SMTP host is set. Set one under the admin email settings.");
        }

        // The sender comes from the settings row, which an admin types into the admin screen — so it
        // is no more trustworthy than the recipient, and it is checked the same way, before any work
        // is done on a message that could never be addressed.
        if (!MailboxAddress.TryParse(settings.FromAddress, out var from))
        {
            m_logger.LogError(
                "Cannot send \"{Subject:l}\": the configured sender address \"{FromAddress}\" is not usable",
                subject, settings.FromAddress);

            return OperationResult<Empty>.BadGateway(
                "Email is misconfigured: the sender address is not a valid email address.");
        }

        from.Name = string.IsNullOrWhiteSpace(settings.FromName) ? settings.FromAddress : settings.FromName;

        // The logo is served from the app's own origin (the SPA ships it in public/), so every
        // template gets it for free rather than each caller remembering to pass it.
        templateData["LogoUrl"] = $"{m_appOptions.TrimmedBaseUrl()}/filehub.png";

        var body = await LoadTemplateAsync(templateName, templateData);
        if (string.IsNullOrEmpty(body))
        {
            return OperationResult<Empty>.Error("Email template could not be loaded.");
        }

        using var email = new MimeMessage();
        email.From.Add(from);
        email.To.Add(to);
        email.Subject = subject;
        email.Body = new TextPart("html") { Text = body };

        try
        {
            m_logger.LogInformation(
                "Sending email \"{Subject:l}\" to {Recipient:l} via {SmtpHost:l}:{Port}",
                subject, recipient, settings.SmtpHost, settings.Port);

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(settings.SmtpHost, settings.Port, settings.SecureSocketOptions);
            if (!string.IsNullOrEmpty(settings.Username))
            {
                await smtp.AuthenticateAsync(settings.Username, settings.Password);
            }

            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
            m_logger.LogInformation("Sent email \"{Subject:l}\" to {Recipient:l}", subject, recipient);
            return OperationResult<Empty>.Success();
        }
        catch (Exception e)
        {
            m_logger.LogError(e, "Failed to send email to {Recipient:l}", recipient);
            return OperationResult<Empty>.BadGateway("The email could not be sent.");
        }
    }

    private async Task<string> LoadTemplateAsync(string templateName, Dictionary<string, string> templateData)
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "EmailTemplates", templateName);
        if (!File.Exists(templatePath))
        {
            m_logger.LogError("Email template not found: {TemplatePath}", templatePath);
            return string.Empty;
        }

        var body = await File.ReadAllTextAsync(templatePath);
        foreach (var (key, value) in templateData)
        {
            body = body.Replace($"@{key}", value, StringComparison.Ordinal);
        }

        return body;
    }
}
