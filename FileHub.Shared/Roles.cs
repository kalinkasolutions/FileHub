namespace Shared;

/// <summary>
/// The two roles seeded on startup. <see cref="Admin"/> gates everything under
/// <c>api/admin</c>; every account holds <see cref="User"/>, which is what browsing and
/// sharing require.
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";

    public static readonly IReadOnlyList<string> All = [Admin, User];
}
