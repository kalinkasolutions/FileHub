using Dal.Extensions;
using Entities.Account;
using Microsoft.AspNetCore.Identity;
using Shared;

namespace FileHub;

/// <summary>
/// Two throwaway accounts with known passwords, so a local run has something to sign in as without
/// going through the bootstrap dance (generated password → forced change → configure SMTP → invite).
///
/// <b>Development only.</b> <see cref="Seed"/> calls this behind an environment check and nothing
/// else does: these are published credentials, and an installation that seeded them would be open
/// to anyone who has read this file. It runs <i>before</i> <c>AdminSeeder</c>, which then finds an
/// admin and leaves the install alone.
///
/// It re-applies the password on every start rather than skipping an account that already exists —
/// the point of a debug seed is that a restart always gets you back in, including after a local
/// experiment changed the password or tripped the lockout.
/// </summary>
public static class DevSeed
{
    private const string AdminUserName = "Admin";
    private const string AdminEmail = "admin@local";

    private const string UserUserName = "User";
    private const string UserEmail = "user@local";

    // S2068 is right — these are hard-coded credentials, which is the entire point of the file.
    // They are only reachable from the Development branch in Seed, and they are printed on the
    // console at every start, so there is nothing here an attacker could not read above.
#pragma warning disable S2068
    private const string AdminPassword = "admin";
    private const string UserPassword = "user";
#pragma warning restore S2068

    public static async Task InitializeAsync(IServiceProvider services, ILogger logger)
    {
        var userManager = services.GetRequiredService<UserManager<FileHubUser>>();

        // The Admin role implies every other one, so it is stored on its own beside User — the
        // same set AdminSeeder gives a real seeded admin. The plain account gets User only, so the
        // CreateShares gate is testable from the side that does not hold it.
        await EnsureAccountAsync(userManager, AdminUserName, AdminEmail, AdminPassword, [Roles.Admin, Roles.User]);
        await EnsureAccountAsync(userManager, UserUserName, UserEmail, UserPassword, [Roles.User]);

        logger.LogWarning(
            "Development seed: <{AdminEmail}> (admin) and <{UserEmail}> exist with the throwaway passwords "
            + "printed on the console. This only ever runs in the Development environment.",
            AdminEmail, UserEmail);

        // Console.Out, not the logger, for the same reason AdminSeeder uses it: Serilog persists
        // everything from Information up into the Logs table, and a password does not belong there
        // even when it is this one.
        WriteCredentials();
    }

    private static async Task EnsureAccountAsync(
        UserManager<FileHubUser> userManager,
        string userName,
        string email,
        string password,
        string[] roles
    )
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            user = await CreateAsync(userManager, userName, email, password);
        }
        else
        {
            await ResetAsync(userManager, user, password);
        }

        var missing = roles.Except(await userManager.GetRolesAsync(user), StringComparer.Ordinal).ToArray();

        if (missing.Length == 0)
        {
            return;
        }

        var roled = await userManager.AddToRolesAsync(user, missing);

        if (!roled.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not give the development account <{email}> its roles: {roled.ToErrorString()}");
        }
    }

    private static async Task<FileHubUser> CreateAsync(
        UserManager<FileHubUser> userManager,
        string userName,
        string email,
        string password
    )
    {
        var user = new FileHubUser
        {
            UserName = userName,
            Email = email,
            // Sign-in requires a confirmed address, and there is no invitation mail to confirm it
            // with. MustChangePassword stays false on purpose: the forced-change gate would answer
            // 403 to everything, which is the opposite of what a debug account is for.
            EmailConfirmed = true,
            MustChangePassword = false
        };

        var created = await userManager.CreateAsync(user, password);

        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not create the development account <{email}>: {created.ToErrorString()}");
        }

        return user;
    }

    private static async Task ResetAsync(UserManager<FileHubUser> userManager, FileHubUser user, string password)
    {
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var reset = await userManager.ResetPasswordAsync(user, token, password);

        if (!reset.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not reset the development account <{user.Email}>: {reset.ToErrorString()}");
        }

        user.EmailConfirmed = true;
        user.MustChangePassword = false;
        user.LockoutEnd = null;

        var updated = await userManager.UpdateAsync(user);

        if (!updated.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not re-enable the development account <{user.Email}>: {updated.ToErrorString()}");
        }
    }

    private static void WriteCredentials()
    {
        Console.Out.WriteLine();
        Console.Out.WriteLine("  ---- FileHub: development accounts ----");
        Console.Out.WriteLine($"  admin:  {AdminEmail} / {AdminPassword}");
        Console.Out.WriteLine($"  user:   {UserEmail} / {UserPassword}");
        Console.Out.WriteLine("  Seeded because ASPNETCORE_ENVIRONMENT is Development, and reset to these");
        Console.Out.WriteLine("  passwords on every start.");
        Console.Out.WriteLine("  ---------------------------------------");
        Console.Out.WriteLine();
        Console.Out.Flush();
    }
}
