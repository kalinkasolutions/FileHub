namespace Dtos.Email;

/// <summary>
/// The SMTP settings as the admin screen shows them. Deliberately has no password field: the
/// stored one is never readable, only replaceable.
/// </summary>
public sealed class EmailSettingDto
{
    public string SmtpHost { get; set; }
    public int Port { get; set; }
    public string Username { get; set; }
    public string FromAddress { get; set; }
    public string FromName { get; set; }

    /// <summary>MailKit's <c>SecureSocketOptions</c> by name.</summary>
    public string SecureSocketOptions { get; set; }

    /// <summary>Whether a password is stored, so the form can say "unchanged" instead of "empty".</summary>
    public bool HasPassword { get; set; }

    /// <summary>
    /// Set by an update that dropped the stored password because the save moved it — a different
    /// host, port or transport, or a username removed. The screen has to be able to say so: the
    /// admin left the field empty meaning "keep it", and it was not kept.
    /// </summary>
    public bool PasswordCleared { get; set; }
}
