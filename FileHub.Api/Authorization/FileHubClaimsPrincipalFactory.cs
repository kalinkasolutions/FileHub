using System.Security.Claims;
using Entities.Account;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Shared;

namespace FileHub.Authorization;

/// <summary>
/// Adds the <see cref="MustChangePasswordClaim"/> claim to the sign-in cookie of an account that
/// still carries a password an admin set, and expands the roles an admin implicitly holds. Putting
/// both in the cookie rather than reading them per request costs nothing at request time and needs
/// no invalidation of its own: changing a password or a role rotates the security stamp, and the
/// account endpoints refresh the sign-in, so the cookie — claims included — is rebuilt from the
/// account as it then stands.
/// </summary>
public sealed class FileHubClaimsPrincipalFactory : UserClaimsPrincipalFactory<FileHubUser, IdentityRole<Guid>>
{
    public const string MustChangePasswordClaim = "must_change_password";

    public FileHubClaimsPrincipalFactory(
        UserManager<FileHubUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IOptions<IdentityOptions> options
    ) : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(FileHubUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        if (user.MustChangePassword)
        {
            identity.AddClaim(new Claim(MustChangePasswordClaim, "true"));
        }

        AddImpliedRoles(identity);

        return identity;
    }

    /// <summary>
    /// The admin role is an implicit grant of every other role, and this is what makes that true for
    /// <c>IsInRole</c> and for every <c>RequireRole</c> policy — both of which read the cookie's role
    /// claims and nothing else. The roles are not stored on the account: expanding them here means a
    /// demotion has one row to remove rather than a set, and cannot leave a granted-looking row
    /// behind.
    /// </summary>
    private void AddImpliedRoles(ClaimsIdentity identity)
    {
        var roleClaimType = Options.ClaimsIdentity.RoleClaimType;
        var stored = identity.FindAll(roleClaimType).Select(c => c.Value).ToList();

        foreach (var role in Roles.Effective(stored))
        {
            if (stored.Contains(role, StringComparer.Ordinal))
            {
                continue;
            }

            identity.AddClaim(new Claim(roleClaimType, role));
        }
    }
}
