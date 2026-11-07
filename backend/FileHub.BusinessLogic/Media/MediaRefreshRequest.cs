namespace FileHub.BusinessLogic.Media;

/// <summary>A title folder whose cached look-up a share link's preview found missing or stale.</summary>
public sealed record MediaRefreshRequest(Guid BasePathId, string Folder);
