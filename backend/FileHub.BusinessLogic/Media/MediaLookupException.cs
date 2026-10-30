namespace FileHub.BusinessLogic.Media;

/// <summary>A source refused or misbehaved in a way worth telling the user about in its own words —
/// a rejected key, most of all. Transport errors stay what they are.</summary>
public sealed class MediaLookupException : Exception
{
    public MediaLookupException(string message) : base(message)
    {
    }
}
