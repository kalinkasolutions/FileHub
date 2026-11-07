namespace FileHub.BusinessLogic.Services.Shares;

/// <summary>A stored poster behind a public link. <see cref="FetchedAt"/> versions the image URL,
/// so a refreshed poster is a new URL to every chat client that cached the old one.</summary>
public sealed record PublicSharePoster(string FullPath, DateTime FetchedAt);
