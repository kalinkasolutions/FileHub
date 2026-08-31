using System.Security.Cryptography;
using Dal.Extensions;
using Entities.Account;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Shared;

namespace FileHub.BusinessLogic.Services.Admin;

/// <summary>
/// Brings an installation that has no administrator back to one. Called from startup on every run
/// and does nothing at all while an admin exists, so a restart never resets a password that has
/// already been changed.
///
/// A static class with its dependencies passed in: startup owns the scope this runs in, and there
/// is nothing here worth a DI registration.
/// </summary>
public static class AdminSeeder
{
    /// <summary>The display name a freshly seeded admin gets, when it is not already taken.</summary>
    public const string DefaultUserName = "Admin";

    /// <param name="credentialOutput">
    /// Where a generated bootstrap password is written — the process's own stdout in production, so
    /// <c>docker compose logs</c> shows it. It deliberately does not go through <see cref="ILogger"/>:
    /// Serilog persists everything from Information up into the <c>Logs</c> table in the very database
    /// this account guards, which would keep the credential readable forever.
    /// </param>
    public static async Task<OperationResult<Empty>> EnsureAdminAsync(
        UserManager<FileHubUser> userManager,
        ILogger logger,
        string configuredEmail,
        string configuredPassword,
        TextWriter credentialOutput
    )
    {
        // Any admin at all is enough: the seeded account may since have been renamed, re-addressed
        // or replaced, and re-creating one from config would be a back door, not a repair.
        var admins = await userManager.GetUsersInRoleAsync(Roles.Admin);
        if (admins.Count > 0)
        {
            return OperationResult<Empty>.Success();
        }

        var email = configuredEmail.Trim();

        // An install with no admin left has to be able to come back, and the usual way it gets into
        // that state is an admin demoting or disabling themselves — which leaves their account
        // holding the configured address. Creating a second account with it fails on the unique
        // index (and on the "Admin" username), so the account that already carries the configured
        // identity is promoted instead of duplicated.
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return await PromoteAsync(userManager, logger, existing);
        }

        return await CreateAsync(userManager, logger, email, configuredPassword, credentialOutput);
    }

    private static async Task<OperationResult<Empty>> PromoteAsync(
        UserManager<FileHubUser> userManager,
        ILogger logger,
        FileHubUser user
    )
    {
        var roled = await userManager.AddToRolesAsync(user, MissingRoles(await userManager.GetRolesAsync(user)));
        if (!roled.Succeeded)
        {
            return OperationResult<Empty>.Error(
                $"Could not give <{user.Email}> the admin roles: {roled.ToErrorString()}");
        }

        // Being in the role is not the same as being able to sign in. An account that is locked out
        // or that never confirmed its address would leave the install exactly as locked out as it
        // was, so recovery clears both — on this one account, the one the operator named in config.
        if (user.LockoutEnd is not null)
        {
            var unlocked = await userManager.SetLockoutEndDateAsync(user, null);
            if (!unlocked.Succeeded)
            {
                return OperationResult<Empty>.Error(
                    $"Could not re-enable <{user.Email}>: {unlocked.ToErrorString()}");
            }
        }

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            var confirmed = await userManager.UpdateAsync(user);
            if (!confirmed.Succeeded)
            {
                return OperationResult<Empty>.Error(
                    $"Could not confirm <{user.Email}>: {confirmed.ToErrorString()}");
            }
        }

        logger.LogWarning(
            "This installation had no administrator left. The existing account <{Email}> holds the configured "
            + "admin address, so it was given the Admin role back, re-enabled and confirmed. Its password is "
            + "unchanged — sign in with it, or use \"forgot password\".", user.Email);

        return OperationResult<Empty>.Success();
    }

    private static async Task<OperationResult<Empty>> CreateAsync(
        UserManager<FileHubUser> userManager,
        ILogger logger,
        string email,
        string configuredPassword,
        TextWriter credentialOutput
    )
    {
        var generated = string.IsNullOrWhiteSpace(configuredPassword);
        var password = generated ? GeneratePassword() : configuredPassword;

        var admin = new FileHubUser
        {
            UserName = await FreeUserNameAsync(userManager),
            Email = email,
            // Nobody can mail this address a confirmation link yet — SMTP is configured from
            // inside the admin area this account exists to open.
            EmailConfirmed = true,
            MustChangePassword = true
        };

        var created = await userManager.CreateAsync(admin, password);
        if (!created.Succeeded)
        {
            return OperationResult<Empty>.Error($"Could not create the admin account: {created.ToErrorString()}");
        }

        var roled = await userManager.AddToRolesAsync(admin, s_adminRoles);
        if (!roled.Succeeded)
        {
            return OperationResult<Empty>.Error($"Could not assign the admin roles: {roled.ToErrorString()}");
        }

        if (!generated)
        {
            logger.LogWarning(
                "Created the initial admin account <{Email}> with the configured bootstrap password. "
                + "It must be changed at first sign-in.", email);

            return OperationResult<Empty>.Success();
        }

        // The log line names the account but never the password: see credentialOutput.
        logger.LogWarning(
            "Created the initial admin account <{Email}> with a generated password. It is printed on the "
            + "container's console output (docker compose logs) and nowhere else, and must be changed at "
            + "first sign-in.", email);

        WriteCredential(credentialOutput, email, password);
        return OperationResult<Empty>.Success();
    }

    private static void WriteCredential(TextWriter credentialOutput, string email, string password)
    {
        credentialOutput.WriteLine();
        credentialOutput.WriteLine("  ---- FileHub: initial administrator ----");
        credentialOutput.WriteLine($"  email:    {email}");
        credentialOutput.WriteLine($"  password: {password}");
        credentialOutput.WriteLine("  This is the only time the password is shown. It is not written to the");
        credentialOutput.WriteLine("  Logs table, so read it here and change it at the first sign-in.");
        credentialOutput.WriteLine("  ----------------------------------------");
        credentialOutput.WriteLine();
        credentialOutput.Flush();
    }

    /// <summary>
    /// The roles the seeded admin is given. Not <see cref="Roles.All"/>: the Admin role already
    /// implies every other one, so storing them as rows would only leave a demotion with more to
    /// clean up than it removed.
    /// </summary>
    private static readonly string[] s_adminRoles = [Roles.Admin, Roles.User];

    private static string[] MissingRoles(IList<string> currentRoles) =>
        s_adminRoles.Except(currentRoles, StringComparer.Ordinal).ToArray();

    /// <summary>
    /// A display name no account holds yet. <c>FindByNameAsync</c> is a lookup for availability
    /// here, not a way to resolve a sign-in — those go by address, always.
    /// </summary>
    private static async Task<string> FreeUserNameAsync(UserManager<FileHubUser> userManager)
    {
        if (await userManager.FindByNameAsync(DefaultUserName) is null)
        {
            return DefaultUserName;
        }

        // Some other account is called "Admin". A suffix keeps seeding working rather than throwing
        // the whole start-up away over a display name.
        return $"{DefaultUserName} {Guid.NewGuid():N}"[..14];
    }

    /// <summary>
    /// A readable random password, well above the configured minimum. The alphabet leaves out the
    /// characters that are misread when a password is copied off a terminal (0/O, 1/l/I).
    /// </summary>
    private static string GeneratePassword()
    {
        const string alphabet = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return RandomNumberGenerator.GetString(alphabet, 20);
    }
}
