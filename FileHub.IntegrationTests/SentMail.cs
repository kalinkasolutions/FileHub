namespace FileHub.IntegrationTests;

public sealed record SentMail(MailKind Kind, string Recipient, Guid? UserId, string Token);
