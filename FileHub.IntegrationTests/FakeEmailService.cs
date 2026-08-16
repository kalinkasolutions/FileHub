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
    private readonly Lock m_lock = new();
    private readonly List<SentMail> m_sent = [];

    public IReadOnlyList<SentMail> Sent
    {
        get
        {
            // The password reset send happens on a background task now, so the list is written from a
            // thread the test is not on.
            lock (m_lock)
            {
                return [.. m_sent];
            }
        }
    }

    public SentMail? Last
    {
        get
        {
            lock (m_lock)
            {
                return m_sent.Count == 0 ? null : m_sent[^1];
            }
        }
    }

    /// <summary>Set to make the next send fail, the way an unreachable SMTP host would.</summary>
    public bool FailSends { get; set; }

    /// <summary>
    /// Runs before a send is recorded. Set it to something that finishes when the test says so and
    /// this fake becomes a slow SMTP server, which is what asserting "the answer does not wait for
    /// delivery" needs.
    /// </summary>
    public Func<Task>? BeforeSend { get; set; }

    /// <summary>
    /// Waits for a mail that is on its way from a background task. Returns as soon as
    /// <paramref name="count"/> mails have been recorded, and fails the test rather than hanging if
    /// they never arrive.
    /// </summary>
    public async Task<SentMail> WaitForMailAsync(int count = 1)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var sent = Sent;
            if (sent.Count >= count)
            {
                return sent[count - 1];
            }

            await Task.Delay(5);
        }

        Assert.Fail($"No mail number {count} was sent within the timeout.");
        return null!;
    }

    public Task<OperationResult<Empty>> SendInviteMailAsync(string recipient, Guid userId, string token) =>
        Record(new SentMail(MailKind.Invite, recipient, userId, token));

    public Task<OperationResult<Empty>> SendResetPasswordMailAsync(string recipient, string token) =>
        Record(new SentMail(MailKind.ResetPassword, recipient, null, token));

    public Task<OperationResult<Empty>> SendChangeEmailMailAsync(string newEmail, Guid userId, string token) =>
        Record(new SentMail(MailKind.ChangeEmail, newEmail, userId, token));

    public Task<OperationResult<Empty>> SendTestMailAsync(string recipient) =>
        Record(new SentMail(MailKind.Test, recipient, null, string.Empty));

    private async Task<OperationResult<Empty>> Record(SentMail mail)
    {
        var beforeSend = BeforeSend;
        if (beforeSend is not null)
        {
            await beforeSend();
        }

        if (FailSends)
        {
            return OperationResult<Empty>.BadGateway("The email could not be sent.");
        }

        lock (m_lock)
        {
            m_sent.Add(mail);
        }

        return OperationResult<Empty>.Success();
    }
}
