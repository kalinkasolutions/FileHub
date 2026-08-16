using Dtos.Email;
using FileHub.BusinessLogic.Email;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

        // The stored secret is replaceable, never readable — the DTO carries only the two booleans
        // that say whether one is there and whether the last save dropped it.
        Assert.DoesNotContain(
            typeof(EmailSettingDto).GetProperties(),
            p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase)
                 && p.Name != nameof(EmailSettingDto.HasPassword)
                 && p.Name != nameof(EmailSettingDto.PasswordCleared));
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

    // ---- the stored password does not follow the settings somewhere else ----

    [Fact]
    public async Task Repointing_the_host_with_an_empty_password_clears_the_stored_one()
    {
        await Settings.GetAsync();

        var dto = Valid(host: "listener.example.net");
        dto.Password = string.Empty;
        var result = await Settings.UpdateAsync(dto);

        // "Keep the stored one" must not survive a change of where the secret is sent: pointing the
        // host at a listener and reading the password off it is the whole attack.
        Assert.Equal(string.Empty, (await Provider.GetAsync()).Password);
        Assert.False(result.Value.HasPassword);
        Assert.True(result.Value.PasswordCleared);
    }

    [Fact]
    public async Task Changing_the_port_with_an_empty_password_clears_the_stored_one()
    {
        await Settings.GetAsync();

        var dto = Valid(port: 2525);
        dto.Password = string.Empty;
        var result = await Settings.UpdateAsync(dto);

        Assert.Equal(string.Empty, (await Provider.GetAsync()).Password);
        Assert.True(result.Value.PasswordCleared);
    }

    [Fact]
    public async Task Downgrading_the_transport_with_an_empty_password_clears_the_stored_one()
    {
        await Settings.GetAsync();

        var dto = Valid(transport: nameof(SecureSocketOptions.None));
        dto.Password = string.Empty;
        var result = await Settings.UpdateAsync(dto);

        // "None" is an accepted transport, so the same host over cleartext would otherwise hand the
        // stored password to anyone on the path.
        Assert.Equal(string.Empty, (await Provider.GetAsync()).Password);
        Assert.True(result.Value.PasswordCleared);
    }

    [Fact]
    public async Task Removing_the_username_clears_the_stored_password()
    {
        await Settings.GetAsync();

        var dto = Valid();
        dto.Username = string.Empty;
        dto.Password = string.Empty;
        var result = await Settings.UpdateAsync(dto);

        // Without a username nothing authenticates, so the screen would be showing a stored password
        // that is never used — and a later username would quietly put it back on the wire.
        Assert.False(result.Value.HasPassword);
        Assert.True(result.Value.PasswordCleared);
    }

    [Fact]
    public async Task Repointing_the_host_and_supplying_the_password_stores_the_new_one()
    {
        await Settings.GetAsync();

        var dto = Valid(host: "mail.example.org");
        dto.Password = "a-new-secret";
        var result = await Settings.UpdateAsync(dto);

        // Moving a server is an ordinary thing to do; it just means saying the password again.
        Assert.Equal("a-new-secret", (await Provider.GetAsync()).Password);
        Assert.False(result.Value.PasswordCleared);
    }

    [Fact]
    public async Task Editing_only_the_sender_name_keeps_the_stored_password()
    {
        await Settings.GetAsync();

        var dto = Valid();
        dto.FromName = "FileHub Mailer";
        dto.Password = string.Empty;
        var result = await Settings.UpdateAsync(dto);

        Assert.Equal(ConfiguredPassword, (await Provider.GetAsync()).Password);
        Assert.False(result.Value.PasswordCleared);
    }

    [Fact]
    public async Task Updating_with_a_password_longer_than_the_column_is_a_validation_error()
    {
        var dto = Valid();
        dto.Password = new string('x', 256);

        var result = await Settings.UpdateAsync(dto);

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(UpdateEmailSettingDto.Password), result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Two_first_reads_at_once_still_seed_a_single_row()
    {
        using var scopeA = NewScope();
        using var scopeB = NewScope();
        var providerA = scopeA.ServiceProvider.GetRequiredService<IEmailSettingsProvider>();
        var providerB = scopeB.ServiceProvider.GetRequiredService<IEmailSettingsProvider>();

        await Task.WhenAll(
            Task.Run(() => providerA.GetOrCreateAsync()),
            Task.Run(() => providerB.GetOrCreateAsync()));

        // Nothing stops a second row at the database level, and a second row is settings an admin
        // edits that nothing reads — the repository hands back the oldest.
        Context.ChangeTracker.Clear();
        Assert.Single(await Context.EmailSettings.ToListAsync());
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
