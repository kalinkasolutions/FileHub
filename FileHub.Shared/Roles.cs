namespace Shared;

/// <summary>
/// The roles seeded on startup. <see cref="Admin"/> gates everything under <c>api/admin</c>; every
/// account holds <see cref="User"/>, which is what browsing requires; <see cref="CreateShares"/> is
/// what publishing a link requires and is granted account by account.
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";

    /// <summary>
    /// Permission to publish a share link. Nobody holds it until an admin grants it — browsing a
    /// disk and handing out an anonymous URL into it are different powers, and this is the second
    /// one. An admin holds it implicitly, through <see cref="Effective"/>.
    /// </summary>
    public const string CreateShares = "CreateShares";

    public static readonly IReadOnlyList<string> All = [Admin, User, CreateShares];

    /// <summary>
    /// The roles an account actually acts with. <see cref="Admin"/> is an implicit grant of every
    /// other role, so it is expanded here rather than stored: an admin who is later demoted has one
    /// row to lose, not a set of rows that a demotion has to remember to clean up.
    ///
    /// This is the one place that expansion happens. The sign-in cookie is built from it (so
    /// <c>IsInRole</c> and every <c>RequireRole</c> policy agree with it), and so is the status the
    /// SPA reads — the client would otherwise hide from an admin a button the API would have
    /// answered.
    /// </summary>
    public static IReadOnlyList<string> Effective(IEnumerable<string> storedRoles)
    {
        var stored = storedRoles.ToList();

        if (!stored.Contains(Admin, StringComparer.Ordinal))
        {
            return stored;
        }

        return All;
    }

    /// <summary>
    /// Whether these roles may publish a share link. Kept next to <see cref="Effective"/> because
    /// the two have to agree about what the admin role implies.
    /// </summary>
    public static bool CanCreateShares(IEnumerable<string> storedRoles)
    {
        var stored = storedRoles.ToList();

        return stored.Contains(Admin, StringComparer.Ordinal)
               || stored.Contains(CreateShares, StringComparer.Ordinal);
    }
}
