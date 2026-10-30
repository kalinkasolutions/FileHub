namespace FileHub.BusinessLogic.Media;

/// <summary>The <c>Media</c> configuration section: where the posters the look-ups bring back are kept.</summary>
public sealed class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>Directory for the poster files, one per cached look-up, named by the row's id.
    /// Relative paths resolve against the working directory, like the connection string.</summary>
    public string PosterPath { get; set; } = "./data/posters";
}
