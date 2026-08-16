namespace Dtos.Admin;

/// <summary>One row of the admin's user list.</summary>
public sealed class UserDto
{
    public Guid Id { get; set; }

    /// <summary>Display name. Not what the user signs in with — that is the email address.</summary>
    public string Username { get; set; }

    public string Email { get; set; }

    /// <summary>
    /// False means the invitation was never accepted: an admin created the account, but nobody has
    /// followed the invitation link yet, so it has no password and cannot sign in. The admin screen
    /// shows such an account as "invited".
    /// </summary>
    public bool EmailConfirmed { get; set; }

    public string[] Roles { get; set; }

    /// <summary>The password was set by an admin, so the account can only change it until it does.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>The account is disabled: its lockout runs into the far future.</summary>
    public bool IsLockedOut { get; set; }

    /// <summary>
    /// How many base paths this account has been granted. Zero means it can see nothing at all:
    /// access is granted per path and absence of a grant is a denial, admins included.
    /// </summary>
    public int BasePathCount { get; set; }

    public DateTime CreatedAt { get; set; }
}
