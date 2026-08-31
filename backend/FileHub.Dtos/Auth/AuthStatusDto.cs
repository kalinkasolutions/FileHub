namespace Dtos.Auth;

/// <summary>
/// What the SPA asks for on start-up to decide what to render: whether there is a session at all,
/// who it belongs to, and whether it may go anywhere but the password screen. It answers an
/// anonymous caller too — everything empty, <see cref="Authenticated"/> false — so the client has
/// one shape to read rather than a 401 to interpret.
/// </summary>
public sealed class AuthStatusDto
{
    public bool Authenticated { get; set; }

    /// <summary>Null while <see cref="Authenticated"/> is false.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Display name; not what the account signs in with.</summary>
    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>Role names, which is how the client decides whether to offer the admin area.</summary>
    public string[] Roles { get; set; } = [];

    public bool MustChangePassword { get; set; }
}
