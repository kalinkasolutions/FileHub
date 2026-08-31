namespace Dtos.Account;

/// <summary>
/// Freshly generated recovery codes. Shown exactly once — only their hashes are kept — so the client
/// is responsible for making the user write them down.
/// </summary>
public sealed class RecoveryCodesDto
{
    public List<string> Codes { get; set; } = [];
}
