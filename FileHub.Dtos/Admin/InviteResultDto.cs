namespace Dtos.Admin;

/// <summary>
/// The outcome of an invitation. The two fields are independent: the account exists as soon as
/// <see cref="UserId"/> is set, even when <see cref="InviteMailSent"/> is false, because a mail
/// that does not go out is an SMTP problem and not a reason to throw the account away. A false
/// value is the admin screen's cue to offer "resend invitation".
/// </summary>
public sealed class InviteResultDto
{
    public Guid UserId { get; set; }
    public bool InviteMailSent { get; set; }

    /// <summary>
    /// Why the invitation did not go out, empty when it did. Without it the admin is told only
    /// that the mail failed and has to go and test the SMTP settings to find out why.
    /// </summary>
    public string InviteMailError { get; set; }
}
