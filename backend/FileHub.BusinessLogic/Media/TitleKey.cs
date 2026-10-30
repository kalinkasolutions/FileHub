using System.Text.RegularExpressions;

namespace FileHub.BusinessLogic.Media;

/// <summary>Letters and digits only, lower case: "Spider-Man" is "spiderman" and "gibt's" is
/// "gibts", so a folder name and a source's title can be compared without caring how either
/// punctuates.</summary>
public static partial class TitleKey
{
    public static string Of(string title) => NotWordChar().Replace(title.ToLowerInvariant(), string.Empty);

    [GeneratedRegex(@"[^\p{L}\p{N}]", RegexOptions.None, 1000)]
    private static partial Regex NotWordChar();
}
