using FileHub.BusinessLogic;
using FileHub.BusinessLogic.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// The real <see cref="EmailService"/> against the real settings row — the one place a caller's
/// address meets MimeKit. Nothing here reaches SMTP: every case is refused before a socket is
/// opened, which is exactly the property being pinned.
/// </summary>
public sealed class EmailServiceAddressTests : EmailSettingsTestBase
{
    public EmailServiceAddressTests() : base(ConfiguredPassword)
    {
    }

    [Fact]
    public async Task An_address_MimeKit_cannot_parse_is_a_clean_failure_rather_than_an_exception()
    {
        var service = NewEmailService();

        var result = await service.SendResetPasswordMailAsync("bad user@example.com", "token");

        // It used to throw a ParseException from outside the try, which came out as a 500 — even on
        // the anonymous forgot-password route, whose contract is that it always succeeds.
        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ada@")]
    [InlineData("bad user@example.com")]
    [InlineData("\"unterminated@example.com")]
    public async Task No_address_makes_a_send_throw(string recipient)
    {
        var service = NewEmailService();

        var result = await service.SendTestMailAsync(recipient);

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task A_sender_address_MimeKit_cannot_parse_fails_the_send_instead_of_throwing()
    {
        await Settings.GetAsync();
        var stored = await Context.EmailSettings.SingleAsync();
        stored.FromAddress = "no reply@example.com";
        await Context.SaveChangesAsync();

        var result = await NewEmailService().SendTestMailAsync("admin@example.com");

        // The sender is admin-typed too, and a bad one is a configuration problem to report, not an
        // unhandled exception on every mail the app sends.
        Assert.Equal(ResultCode.BadGateway, result.ResultCode);
    }

    private EmailService NewEmailService() => new(
        Provider,
        Options.Create(new AppOptions { BaseUrl = "https://filehub.example.com" }),
        NullLogger<EmailService>.Instance);
}
