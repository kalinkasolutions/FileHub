using FileHub.BusinessLogic.Email;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// Stands in for SMTP and records what was sent. The tokens matter: a test replays the captured
/// invite / reset / email-change token through the real anonymous endpoint, so the whole two-step
/// flow is exercised rather than just the half that generates the link.
/// </summary>
public sealed class FakeEmailService : IEmailService
{
    public List<SentMail> Sent { get; } = [];

    public SentMail? Last => Sent.Count == 0 ? null : Sent[^1];

    /// <summary>Set to make the next send fail, the way an unreachable SMTP host would.</summary>
    public bool FailSends { get; set; }

    public Task<OperationResult<Empty>> SendInviteMailAsync(string recipient, Guid userId, string token) =>
        Record(new SentMail(MailKind.Invite, recipient, userId, token));

    public Task<OperationResult<Empty>> SendResetPasswordMailAsync(string recipient, string token) =>
        Record(new SentMail(MailKind.ResetPassword, recipient, null, token));

    public Task<OperationResult<Empty>> SendChangeEmailMailAsync(string newEmail, Guid userId, string token) =>
        Record(new SentMail(MailKind.ChangeEmail, newEmail, userId, token));

    public Task<OperationResult<Empty>> SendTestMailAsync(string recipient) =>
        Record(new SentMail(MailKind.Test, recipient, null, string.Empty));

    private Task<OperationResult<Empty>> Record(SentMail mail)
    {
        if (FailSends)
        {
            return Task.FromResult(OperationResult<Empty>.BadGateway("The email could not be sent."));
        }

        Sent.Add(mail);
        return Task.FromResult(OperationResult<Empty>.Success());
    }
}
