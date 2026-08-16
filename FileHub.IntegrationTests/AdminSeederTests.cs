using Entities.Account;
using FileHub.BusinessLogic.Services.Admin;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// Start-up's admin seeding: the account an empty database gets, how a generated password reaches
/// the operator, and — the part that matters most — how an installation that has lost its last
/// admin comes back. There is no registration page and no self-service way into the admin area, so
/// if this cannot repair it, nothing in the app can.
/// </summary>
public sealed class AdminSeederTests : TestHostBase
{
    private const string AdminEmail = "admin@filehub.local";

    private readonly RecordingLogger m_logger = new();
    private readonly StringWriter m_console = new();

    public AdminSeederTests() : base(_ => { })
    {
    }

    [Fact]
    public async Task An_empty_install_gets_an_admin_who_must_change_their_password()
    {
        await EnsureRolesAsync();

        var result = await SeedAsync();

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var admin = await UserManager.FindByEmailAsync(AdminEmail);
        Assert.NotNull(admin);
        Assert.True(admin.EmailConfirmed);
        Assert.True(admin.MustChangePassword);
        Assert.Contains(Shared.Roles.Admin, await UserManager.GetRolesAsync(admin));
        Assert.Contains(Shared.Roles.User, await UserManager.GetRolesAsync(admin));
    }

    [Fact]
    public async Task An_install_that_already_has_an_admin_is_left_alone()
    {
        await CreateUserAsync("someone-else@example.com", "test-password", Shared.Roles.Admin, Shared.Roles.User);

        await SeedAsync();

        // The seeded account may since have been renamed or replaced; re-creating one from config
        // would be a back door, not a repair.
        Assert.Null(await UserManager.FindByEmailAsync(AdminEmail));
        Assert.Equal(string.Empty, m_console.ToString());
    }

    // ---- recovering an install with no admin left ----

    [Fact]
    public async Task The_account_holding_the_configured_address_is_promoted_rather_than_duplicated()
    {
        // Exactly what a self-demotion leaves behind: the configured address and the "Admin" name
        // are taken, by an account that no longer holds the role. Creating a second one fails on the
        // unique index, which is what used to crash-loop the container.
        var demoted = await CreateUserAsync(AdminEmail, "test-password", Shared.Roles.User);
        demoted.UserName = AdminSeeder.DefaultUserName;
        await UserManager.UpdateAsync(demoted);

        var result = await SeedAsync();

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var admins = await UserManager.GetUsersInRoleAsync(Shared.Roles.Admin);
        Assert.Equal(demoted.Id, Assert.Single(admins).Id);
        Assert.Single(Context.Users);
    }

    [Fact]
    public async Task A_promoted_account_is_re_enabled_and_confirmed_so_it_can_actually_sign_in()
    {
        var demoted = await CreateUserAsync(AdminEmail, "test-password", Shared.Roles.User);
        demoted.EmailConfirmed = false;
        await UserManager.SetLockoutEnabledAsync(demoted, true);
        await UserManager.SetLockoutEndDateAsync(demoted, DateTimeOffset.UtcNow.AddYears(100));
        await UserManager.UpdateAsync(demoted);

        var result = await SeedAsync();

        // Being in the role is not the same as being able to sign in, and an install that cannot
        // sign in is exactly as locked out as one with no admin at all.
        Assert.True(result.IsSuccess, result.ErrorMessage);
        var reloaded = await ReloadAsync(demoted.Id);
        Assert.True(reloaded.EmailConfirmed);
        Assert.False(await UserManager.IsLockedOutAsync(reloaded));
    }

    [Fact]
    public async Task A_promoted_account_keeps_its_password_and_is_reported_in_the_log()
    {
        var demoted = await CreateUserAsync(AdminEmail, "test-password", Shared.Roles.User);

        await SeedAsync();

        Assert.True(await UserManager.CheckPasswordAsync(await ReloadAsync(demoted.Id), "test-password"));
        Assert.Contains(m_logger.Messages, m => m.Contains("no administrator left", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_username_someone_else_already_holds_does_not_stop_the_seeding()
    {
        await EnsureRolesAsync();
        var squatter = await CreateUserAsync("someone-else@example.com", "test-password", Shared.Roles.User);
        squatter.UserName = AdminSeeder.DefaultUserName;
        await UserManager.UpdateAsync(squatter);

        var result = await SeedAsync();

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var admin = await UserManager.FindByEmailAsync(AdminEmail);
        Assert.NotNull(admin);
        Assert.NotEqual(AdminSeeder.DefaultUserName, admin.UserName);
    }

    // ---- where the generated password goes ----

    [Fact]
    public async Task A_generated_password_is_printed_on_the_console_and_is_the_real_credential()
    {
        await EnsureRolesAsync();

        await SeedAsync();

        // The console is what `docker compose logs` shows, and this is the only place the password
        // is ever readable.
        var printed = PrintedPassword();
        Assert.NotEmpty(printed);
        Assert.True(await UserManager.CheckPasswordAsync((await UserManager.FindByEmailAsync(AdminEmail))!, printed));
    }

    [Fact]
    public async Task A_generated_password_is_never_logged()
    {
        await EnsureRolesAsync();

        await SeedAsync();

        // Serilog writes everything from Information up into the Logs table in this very database,
        // so a password that reaches the logger is a stored credential, readable forever.
        var printed = PrintedPassword();
        Assert.DoesNotContain(m_logger.Messages, m => m.Contains(printed, StringComparison.Ordinal));
        Assert.Contains(m_logger.Messages, m => m.Contains("generated password", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_configured_password_is_not_printed_at_all()
    {
        await EnsureRolesAsync();

        await SeedAsync("a-configured-password");

        // The operator already has it; echoing it would only put it in one more place.
        Assert.DoesNotContain("a-configured-password", m_console.ToString(), StringComparison.Ordinal);
    }

    private Task<OperationResult<Empty>> SeedAsync(string configuredPassword = "") =>
        AdminSeeder.EnsureAdminAsync(UserManager, m_logger, AdminEmail, configuredPassword, m_console);

    /// <summary>The password as the operator reads it off the container's output.</summary>
    private string PrintedPassword()
    {
        var line = m_console.ToString()
            .Split(Environment.NewLine)
            .FirstOrDefault(l => l.Contains("password:", StringComparison.Ordinal));

        return line is null ? string.Empty : line.Split("password:")[1].Trim();
    }

    private async Task<FileHubUser> ReloadAsync(Guid userId)
    {
        Context.ChangeTracker.Clear();
        return (await UserManager.FindByIdAsync(userId.ToString()))!;
    }
}
