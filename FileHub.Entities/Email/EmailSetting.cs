namespace Entities.Email;

/// <summary>
/// The SMTP configuration, editable by an admin at runtime. Exactly one row exists; it is created
/// from the <c>Email</c> configuration section the first time it is read, so an install that
/// configures SMTP by environment keeps working without anyone opening the admin screen.
/// </summary>
public sealed class EmailSetting : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }

    public string SmtpHost { get; set; }
    public int Port { get; set; }
    public string Username { get; set; }

    /// <summary>
    /// The SMTP password, encrypted with ASP.NET Data Protection (key ring on the mounted volume).
    /// It is never returned by the API, only replaced.
    /// </summary>
    public string ProtectedPassword { get; set; }

    public string FromAddress { get; set; }
    public string FromName { get; set; }

    /// <summary>MailKit's <c>SecureSocketOptions</c> by name: None, Auto, SslOnConnect, StartTls, StartTlsWhenAvailable.</summary>
    public string SecureSocketOptions { get; set; }
}
