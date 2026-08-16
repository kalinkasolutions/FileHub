using Microsoft.AspNetCore.Identity;

namespace Entities.Account;

public sealed class FileHubUser : IdentityUser<Guid>, IBaseEntity
{
    /// <summary>
    /// Set on the seeded admin and on any account whose password was set by an admin rather than
    /// by the account holder. While it is true every authenticated endpoint except the account and
    /// sign-out ones answers 403, so the only thing the session can do is change the password.
    /// </summary>
    public bool MustChangePassword { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}
