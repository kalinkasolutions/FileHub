namespace FileHub.BusinessLogic.Media.Igdb;

/// <summary>A platform has a short form ("PS5", "PC") and a long one ("PlayStation 5"); the popover
/// shows the short one when there is one.</summary>
public sealed record IgdbPlatform
{
    public string? Abbreviation { get; init; }

    public string? Name { get; init; }

    public string? Label => string.IsNullOrEmpty(Abbreviation) ? Name : Abbreviation;
}
