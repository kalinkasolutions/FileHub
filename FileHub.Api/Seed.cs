using Dal;
using Dal.Extensions;
using Entities.Account;
using FileHub.BusinessLogic.Services.Admin;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared;

namespace FileHub;

/// <summary>
/// Brings an empty database up to a usable install: schema, both roles, and one admin account.
/// Runs on every start and is idempotent — an existing admin is left exactly as it is, so a
/// restart never resets a password that has already been changed. The admin half lives in
/// <see cref="AdminSeeder"/>, which is also what repairs an install that has lost its last admin.
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

        // Console.Out, not the logger: a generated bootstrap password has to reach the operator
        // through `docker compose logs` without landing in the Logs table, which lives in the same
        // database the account guards. Serilog's console sink writes to the same stream, so the two
        // still come out interleaved in the container's output.
        var result = await AdminSeeder.EnsureAdminAsync(
            userManager, logger, options.Email, options.Password, Console.Out);

        if (result.HasError)
        {
            // Startup failures are fatal on purpose: an install without a usable admin is worse
            // than one that will not start.
            throw new InvalidOperationException(result.ErrorMessage);
        }
    }
}
