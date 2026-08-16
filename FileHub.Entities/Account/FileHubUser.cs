using Microsoft.AspNetCore.Identity;

namespace Entities.Account;

public sealed class FileHubUser : IdentityUser<Guid>, IBaseEntity
{
    /// <summary>
    /// Set on the seeded admin — the one account whose password an admin knows, because start-up
    /// generated it. There is no route by which an admin sets anybody else's password: every other
    /// account gets its first one from the invitation link. While this is true every authenticated
    /// endpoint except the account and sign-out ones answers 403, so the only thing the session can
    /// do is change the password.
    /// </summary>
    public bool MustChangePassword { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}
