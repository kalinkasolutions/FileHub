using Dal;
using Dal.Extensions;
using Entities.Account;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared;

namespace FileHub;

/// <summary>
/// Brings an empty database up to a usable install: schema, both roles, and one admin account.
/// Runs on every start and is idempotent — an existing admin is left exactly as it is, so a
/// restart never resets a password that has already been changed.
/// </summary>
public static class Seed
{
    public static async Task InitializeAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(Seed));

        await MigrateAsync(services, logger);
        await EnsureRolesAsync(services, logger);
        await EnsureAdminAsync(services, logger);
    }

    private static async Task MigrateAsync(IServiceProvider services, ILogger logger)
    {
        var db = services.GetRequiredService<FileHubContext>();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();

        if (pending.Count > 0)
        {
            logger.LogInformation(
                "Applying {Count} pending database migration(s): {Migrations}",
                pending.Count, string.Join(", ", pending));
        }
        else
        {
            logger.LogInformation("Database is up to date; no migrations to apply");
        }

        await db.Database.MigrateAsync();
    }

    private static async Task EnsureRolesAsync(IServiceProvider services, ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var role in Roles.All)
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole<Guid>(role));

            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Could not create the {role} role: {result.ToErrorString()}");
            }

            logger.LogInformation("Created role {Role}", role);
        }
    }

    private static async Task EnsureAdminAsync(IServiceProvider services, ILogger logger)
    {
        var userManager = services.GetRequiredService<UserManager<FileHubUser>>();
        var options = services.GetRequiredService<IOptions<AdminOptions>>().Value;

        // Any admin at all is enough: the seeded account may since have been renamed, re-addressed
        // or replaced, and re-creating one from config would be a back door, not a repair.
        var admins = await userManager.GetUsersInRoleAsync(Roles.Admin);

        if (admins.Count > 0)
        {
            return;
        }

        var email = options.Email.Trim();
        var admin = new FileHubUser
        {
            UserName = "Admin",
            Email = email,
            // Nobody can mail this address a confirmation link yet — SMTP is configured from
            // inside the admin area this account exists to open.
            EmailConfirmed = true,
            MustChangePassword = true
        };

        var created = await userManager.CreateAsync(admin, options.Password);

        if (!created.Succeeded)
        {
            throw new InvalidOperationException($"Could not create the admin account: {created.ToErrorString()}");
        }

        var roled = await userManager.AddToRolesAsync(admin, [Roles.Admin, Roles.User]);

        if (!roled.Succeeded)
        {
            throw new InvalidOperationException($"Could not assign the admin roles: {roled.ToErrorString()}");
        }

        logger.LogWarning(
            "Created the initial admin account <{Email}> with the configured bootstrap password. " +
            "It must be changed at first sign-in.", email);
    }
}
