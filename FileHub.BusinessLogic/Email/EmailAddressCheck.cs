using MimeKit;

namespace FileHub.BusinessLogic.Email;

/// <summary>
/// Whether an address can actually be put on a message. DataAnnotations' <c>[EmailAddress]</c> is
/// far more permissive than MimeKit's parser — <c>"bad user@example.com"</c> passes the attribute
/// and throws a <see cref="ParseException"/> in <c>MailboxAddress.Parse</c> — so anything that will
/// end up in a <c>MimeMessage</c> is checked here first, with the very parser that is going to read
/// it.
/// </summary>
public static class EmailAddressCheck
{
    public static bool IsSendable(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        return MailboxAddress.TryParse(address, out _);
    }
}
