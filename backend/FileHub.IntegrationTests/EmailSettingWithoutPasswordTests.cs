using Dtos.Email;
using Microsoft.EntityFrameworkCore;

namespace FileHub.IntegrationTests;

/// <summary>
/// The state a fresh install starts in: an <c>Email</c> section with no password, or none at all.
/// It needs its own fixture because the configured password is fixed when the provider is built.
/// </summary>
public sealed class EmailSettingWithoutPasswordTests : EmailSettingsTestBase
{
    public EmailSettingWithoutPasswordTests() : base(string.Empty)
    {
    }

    [Fact]
    public async Task No_configured_password_is_stored_as_an_empty_column()
    {
        await Settings.GetAsync();

        var stored = await Context.EmailSettings.AsNoTracking().SingleAsync();

        // Protecting an empty string would still produce a blob, which HasPassword would then read
        // as "a password is stored".
        Assert.Equal(string.Empty, stored.ProtectedPassword);
    }

    [Fact]
    public async Task No_configured_password_reads_back_as_no_password()
    {
        var result = await Settings.GetAsync();

        Assert.False(result.Value.HasPassword);
        Assert.Equal(string.Empty, (await Provider.GetAsync()).Password);
    }

    [Fact]
    public async Task An_admin_can_set_the_first_password_from_the_screen()
    {
        await Settings.GetAsync();

        var result = await Settings.UpdateAsync(new UpdateEmailSettingDto
        {
            SmtpHost = "smtp.example.com",
            Port = 587,
            Username = "postmaster",
            Password = "first-secret",
            FromAddress = "filehub@example.com",
            FromName = "FileHub",
            SecureSocketOptions = "StartTls"
        });

        Assert.True(result.Value.HasPassword);
        Assert.Equal("first-secret", (await Provider.GetAsync()).Password);
    }
}
