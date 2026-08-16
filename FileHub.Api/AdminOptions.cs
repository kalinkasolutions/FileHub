namespace FileHub;

/// <summary>
/// The account <see cref="Seed"/> creates on an empty database, bound from the <c>Admin</c>
/// configuration section (<c>Admin__Email</c> / <c>Admin__Password</c> in docker-compose).
/// The seeded password is a bootstrap credential only: the account is created with
/// <c>MustChangePassword</c> set, so the first thing that session can do is replace it.
/// </summary>
public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    public string Email { get; set; } = "admin@filehub.local";

    /// <summary>
    /// Left empty on purpose. An unset password makes <see cref="Seed"/> generate a random one and
    /// write it to the log once — a shipped default would be a published credential on an
    /// installation that is reachable from the internet before anyone has signed in.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
