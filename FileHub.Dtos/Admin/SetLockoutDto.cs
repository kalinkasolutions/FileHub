namespace Dtos.Admin;

/// <summary>Disables (<c>true</c>) or re-enables (<c>false</c>) an account.</summary>
public sealed class SetLockoutDto
{
    public bool Locked { get; set; }
}
