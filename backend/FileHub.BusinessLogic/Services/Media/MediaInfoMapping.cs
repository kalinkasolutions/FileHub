using Dtos.Media;
using Entities.Media;

namespace FileHub.BusinessLogic.Services.Media;

public static class MediaInfoMapping
{
    public static MediaInfoDto ToDto(MediaInfo info) => new()
    {
        Id = info.Id,
        Found = info.Found,
        Query = info.Query,
        Source = info.Source,
        MediaType = info.MediaType,
        Title = info.Title,
        Year = info.Year,
        Overview = info.Overview,
        Rating = info.Rating,
        Genres = Split(info.Genres),
        Platforms = Split(info.Platforms),
        HasPoster = info.HasPoster,
        ExternalUrl = info.ExternalUrl,
        FetchedAt = info.FetchedAt
    };

    private static List<string> Split(string joined) =>
        string.IsNullOrEmpty(joined) ? [] : joined.Split(", ").ToList();
}
