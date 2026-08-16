namespace Dtos.Files;

public sealed class NavigationDto
{
    /// <summary>Display name of the directory that was navigated into.</summary>
    public string NavigationName { get; set; }

    public List<FileEntryDto> Entries { get; set; } = [];
}
