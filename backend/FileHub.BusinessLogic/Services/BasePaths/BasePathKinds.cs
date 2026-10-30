using Entities.Paths;

namespace FileHub.BusinessLogic.Services.BasePaths;

/// <summary>The kind on the wire is a word, not a number: "none", "media" or "games".</summary>
public static class BasePathKinds
{
    public static string Name(BasePathKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>Empty means none. Anything that is not one of the three words is not a kind.</summary>
    public static bool TryParse(string? name, out BasePathKind kind)
    {
        var trimmed = (name ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            kind = BasePathKind.None;
            return true;
        }

        return Enum.TryParse(trimmed, ignoreCase: true, out kind) && Enum.IsDefined(kind);
    }
}
