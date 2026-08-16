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
}
