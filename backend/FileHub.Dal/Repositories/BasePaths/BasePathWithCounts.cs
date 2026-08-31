using Entities.Paths;

namespace Dal.Repositories.BasePaths;

/// <summary>
/// A base path together with the two numbers the admin list shows. Exists so the list is one query
/// with two projected counts rather than two collection Includes: including both makes EF join the
/// grant tables against each other, which multiplies the rows out and is what
/// MultipleCollectionIncludeWarning is about. The counts are all the caller wants; the grant rows
/// themselves are read from their own screens.
/// </summary>
public sealed class BasePathWithCounts
{
    public required BasePath BasePath { get; init; }
    public required int UserCount { get; init; }
    public required int GroupCount { get; init; }
}
