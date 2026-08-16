using Dtos.Email;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// The one SMTP settings row: how it comes into existence, and what the admin screen may and may
/// not see of it.
/// </summary>
public sealed class EmailSettingTests : EmailSettingsTestBase
{
    public EmailSettingTests() : base(ConfiguredPassword)
    {
    }

    [Fact]
    public async Task The_row_is_seeded_from_configuration_on_first_read()
    {
        Assert.Empty(Context.EmailSettings);

        var result = await Settings.GetAsync();

        // An install configured purely by environment sends mail without anyone opening the admin
        // screen; the first edit there takes over from the section.
        Assert.True(result.IsSuccess);
        Assert.Equal("smtp.example.com", result.Value.SmtpHost);
        Assert.Equal(587, result.Value.Port);
        Assert.Equal("postmaster", result.Value.Username);
        Assert.Equal("filehub@example.com", result.Value.FromAddress);
        Assert.Equal(nameof(SecureSocketOptions.StartTls), result.Value.SecureSocketOptions);
    }

    [Fact]
    public async Task The_row_is_seeded_only_once()
    {
        await Settings.GetAsync();
        await Settings.GetAsync();

        Assert.Single(await Context.EmailSettings.ToListAsync());
    }

    [Fact]
    public async Task The_seeded_password_is_stored_encrypted()
    {
        await Settings.GetAsync();

        var stored = await Context.EmailSettings.AsNoTracking().SingleAsync();

        Assert.NotEmpty(stored.ProtectedPassword);
        Assert.DoesNotContain(ConfiguredPassword, stored.ProtectedPassword, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_stored_password_round_trips_through_data_protection()
    {
        await Settings.GetAsync();

        var resolved = await Provider.GetAsync();

        Assert.Equal(ConfiguredPassword, resolved.Password);
    }

    [Fact]
    public async Task The_settings_the_admin_screen_reads_never_carry_the_password()
    {
        var result = await Settings.GetAsync();

        // The stored secret is replaceable, never readable — the DTO has no field for it at all.
        Assert.DoesNotContain(
            typeof(EmailSettingDto).GetProperties(),
            p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase)
                 && p.Name != nameof(EmailSettingDto.HasPassword));
        Assert.True(result.Value.HasPassword);
    }

    [Fact]
    public async Task Updating_writes_the_host_and_the_transport()
    {
        await Settings.GetAsync();

        var result = await Settings.UpdateAsync(Valid(host: "mail.example.org", port: 465, transport: "SslOnConnect"));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal("mail.example.org", result.Value.SmtpHost);
        Assert.Equal(465, result.Value.Port);
        Assert.Equal("SslOnConnect", result.Value.SecureSocketOptions);
        Assert.Equal(SecureSocketOptions.SslOnConnect, (await Provider.GetAsync()).SecureSocketOptions);
    }

    [Fact]
    public async Task Updating_trims_the_fields_it_writes()
    {
        await Settings.GetAsync();

        var dto = Valid();
        dto.SmtpHost = "  mail.example.org  ";
        dto.Username = "  postmaster  ";
        var result = await Settings.UpdateAsync(dto);

        Assert.Equal("mail.example.org", result.Value.SmtpHost);
        Assert.Equal("postmaster", result.Value.Username);
    }

    [Fact]
    public async Task Updating_with_an_empty_password_keeps_the_stored_one()
    {
        await Settings.GetAsync();

        var dto = Valid();
        dto.Password = string.Empty;
        await Settings.UpdateAsync(dto);

        // The screen cannot read the secret back, so requiring it would mean retyping it to change
        // the host.
        Assert.Equal(ConfiguredPassword, (await Provider.GetAsync()).Password);
    }

    [Fact]
    public async Task Updating_with_an_empty_password_still_reports_that_one_is_stored()
    {
        await Settings.GetAsync();

        var dto = Valid();
        dto.Password = string.Empty;
        var result = await Settings.UpdateAsync(dto);

        Assert.True(result.Value.HasPassword);
    }

    [Fact]
    public async Task Updating_with_a_new_password_replaces_the_stored_one()
    {
        await Settings.GetAsync();

        var dto = Valid();
        dto.Password = "a-new-secret";
        await Settings.UpdateAsync(dto);

        Assert.Equal("a-new-secret", (await Provider.GetAsync()).Password);
    }

    [Fact]
    public async Task Updating_a_new_password_stores_it_encrypted()
    {
        await Settings.GetAsync();

        var dto = Valid();
        dto.Password = "a-new-secret";
        await Settings.UpdateAsync(dto);

        var stored = await Context.EmailSettings.AsNoTracking().SingleAsync();
        Assert.DoesNotContain("a-new-secret", stored.ProtectedPassword, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Updating_seeds_the_row_when_it_has_never_been_read()
    {
        var result = await Settings.UpdateAsync(Valid(host: "mail.example.org"));

        Assert.True(result.IsSuccess);
        Assert.Single(await Context.EmailSettings.ToListAsync());
    }

    [Fact]
    public async Task Updating_without_a_host_is_a_validation_error()
    {
        var dto = Valid();
        dto.SmtpHost = string.Empty;

        var result = await Settings.UpdateAsync(dto);

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(UpdateEmailSettingDto.SmtpHost), result.ValidationErrors.Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public async Task Updating_with_a_port_outside_the_valid_range_is_a_validation_error(int port)
    {
        var result = await Settings.UpdateAsync(Valid(port: port));

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(UpdateEmailSettingDto.Port), result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Updating_with_a_transport_name_MailKit_does_not_know_is_a_validation_error()
    {
        var result = await Settings.UpdateAsync(Valid(transport: "MaybeTls"));

        // Caught on the screen that made the typo, rather than silently falling back to Auto on the
        // next send.
        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(UpdateEmailSettingDto.SecureSocketOptions), result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Updating_with_a_malformed_from_address_is_a_validation_error()
    {
        var dto = Valid();
        dto.FromAddress = "not-an-address";

        var result = await Settings.UpdateAsync(dto);

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(UpdateEmailSettingDto.FromAddress), result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task A_transport_name_the_row_no_longer_matches_resolves_to_auto()
    {
        await Settings.GetAsync();
        var stored = await Context.EmailSettings.SingleAsync();
        stored.SecureSocketOptions = "SomethingRemoved";
        await Context.SaveChangesAsync();

        var resolved = await Provider.GetAsync();

        Assert.Equal(SecureSocketOptions.Auto, resolved.SecureSocketOptions);
    }

    [Fact]
    public async Task Settings_with_a_host_count_as_configured()
    {
        var resolved = await Provider.GetAsync();

        Assert.True(resolved.IsConfigured);
    }

    [Fact]
    public async Task Sending_a_test_message_goes_to_the_given_recipient()
    {
        var result = await Settings.SendTestAsync(new SendTestEmailDto { Recipient = "  admin@example.com  " });

        Assert.True(result.IsSuccess);
        Assert.Equal(MailKind.Test, Email.Last!.Kind);
        Assert.Equal("admin@example.com", Email.Last.Recipient);
    }

    [Fact]
    public async Task Sending_a_test_message_reports_an_smtp_failure()
    {
        Email.FailSends = true;

        var result = await Settings.SendTestAsync(new SendTestEmailDto { Recipient = "admin@example.com" });

        // The whole point of the button is to find out whether the settings work.
        Assert.Equal(ResultCode.BadGateway, result.ResultCode);
    }

    [Fact]
    public async Task Sending_a_test_message_to_a_malformed_address_is_a_validation_error()
    {
        var result = await Settings.SendTestAsync(new SendTestEmailDto { Recipient = "not-an-address" });

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Empty(Email.Sent);
    }

    private static UpdateEmailSettingDto Valid(
        string host = "smtp.example.com", int port = 587, string transport = "StartTls") => new()
    {
        SmtpHost = host,
        Port = port,
        Username = "postmaster",
        Password = "kept-or-replaced",
        FromAddress = "filehub@example.com",
        FromName = "FileHub",
        SecureSocketOptions = transport
    };
}
