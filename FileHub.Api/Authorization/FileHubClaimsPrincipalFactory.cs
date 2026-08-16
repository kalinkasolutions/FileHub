using System.Security.Claims;
using Entities.Account;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace FileHub.Authorization;

/// <summary>
/// Adds the <see cref="MustChangePasswordClaim"/> claim to the sign-in cookie of an account that
/// still carries a password an admin set. Putting it in the cookie rather than reading
/// <c>MustChangePassword</c> per request costs nothing at request time and needs no invalidation of
/// its own: changing the password rotates the security stamp, and the account endpoints refresh the
/// sign-in, so the cookie — and with it the claim — is rebuilt from the flag as it now stands.
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

        return identity;
    }
}
