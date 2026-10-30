namespace Entities.Paths;

/// <summary>
/// What the folders directly under a base path are, which decides where FileHub looks their
/// details up: TMDb for movies and series, IGDB for games. <see cref="None"/> is a plain disk with
/// no info button at all.
/// </summary>
public enum BasePathKind
{
    None = 0,
    Media = 1,
    Games = 2
}
