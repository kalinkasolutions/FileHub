using System.Text.Json;
using Dal.Repositories.BasePaths;
using Dal.Repositories.Media;
using Dtos.Media;
using Entities.Media;
using Entities.Paths;
using FileHub.BusinessLogic.Auditing;
using FileHub.BusinessLogic.Authorization;
using FileHub.BusinessLogic.Media;
using Microsoft.Extensions.Logging;
using Shared;

namespace FileHub.BusinessLogic.Services.Media;

public sealed class MediaInfoService : IMediaInfoService
{
    /// <summary>TMDb's terms allow a cached answer for six months and no longer; IGDB's do not
    /// mind, but one rule is simpler than two and a game's rating moves too.</summary>
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(180);

    private readonly ILogger<MediaInfoService> m_logger;
    private readonly IAuditActor m_auditActor;
    private readonly IBasePathRepository m_basePathRepository;
    private readonly IMediaInfoRepository m_mediaInfoRepository;
    private readonly IReadOnlyList<IMediaLookupProvider> m_providers;
    private readonly IPosterFetcher m_posterFetcher;
    private readonly IPosterStore m_posterStore;

    public MediaInfoService(
        ILogger<MediaInfoService> logger,
        IAuditActor auditActor,
        IBasePathRepository basePathRepository,
        IMediaInfoRepository mediaInfoRepository,
        IEnumerable<IMediaLookupProvider> providers,
        IPosterFetcher posterFetcher,
        IPosterStore posterStore
    )
    {
        m_logger = logger;
        m_auditActor = auditActor;
        m_basePathRepository = basePathRepository;
        m_mediaInfoRepository = mediaInfoRepository;
        m_providers = providers.ToList();
        m_posterFetcher = posterFetcher;
        m_posterStore = posterStore;
    }

    public async Task<OperationResult<MediaInfoDto>> GetAsync(
        Guid userId, bool callerIsAdmin, Guid basePathId, string relativePath, bool refresh,
        CancellationToken cancellationToken)
    {
        var basePath = await m_basePathRepository.GetForUserAsync(basePathId, userId, callerIsAdmin);
        if (basePath is null)
        {
            return NotGranted<MediaInfoDto>(userId, basePathId);
        }

        if (basePath.Kind == BasePathKind.None)
        {
            return OperationResult<MediaInfoDto>.BadRequest("This disk is not marked as media or games, so there is nothing to look up");
        }

        var folder = relativePath ?? string.Empty;

        // Only the folders directly under the base path are titles; "season_01" two levels down is
        // not, and neither is the base path itself.
        if (folder.Length == 0 || folder.Contains('/') || folder.Contains('\\'))
        {
            return OperationResult<MediaInfoDto>.BadRequest("Only a folder directly under the disk has info");
        }

        if (!PathSandbox.TryResolve(basePath.Path, folder, out var fullPath) || !Directory.Exists(fullPath))
        {
            return OperationResult<MediaInfoDto>.NotFound("Path not found");
        }

        var provider = m_providers.FirstOrDefault(p => p.Kind == basePath.Kind)
                       ?? throw new InvalidOperationException($"No look-up provider is registered for {basePath.Kind}");

        var cached = await m_mediaInfoRepository.GetAsync(basePathId, folder);
        if (cached is not null && !refresh && DateTime.UtcNow - cached.FetchedAt < MaxAge)
        {
            return OperationResult<MediaInfoDto>.Success(Map(cached));
        }

        if (!provider.IsConfigured)
        {
            // A stale answer is still an answer; only a first look-up has nothing to fall back on.
            return cached is not null
                ? OperationResult<MediaInfoDto>.Success(Map(cached))
                : OperationResult<MediaInfoDto>.BadRequest($"{provider.Name} is not configured: {provider.NotConfiguredHint}");
        }

        var parsed = FolderTitleParser.Parse(folder);
        MediaLookup? lookup;

        try
        {
            lookup = await provider.LookupAsync(parsed, cancellationToken);
        }
        catch (MediaLookupException exception)
        {
            m_logger.LogWarning(exception, "{Actor:l} looked up \"{Folder:l}\" on {Source:l}: {Reason:l}",
                m_auditActor.Describe(), folder, provider.Name, exception.Message);
            return OperationResult<MediaInfoDto>.BadGateway(exception.Message);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            // Not cached: the next ask tries again, which is what a network blip wants.
            m_logger.LogWarning(exception, "{Actor:l} looked up \"{Folder:l}\" on {Source:l}, which did not answer",
                m_auditActor.Describe(), folder, provider.Name);
            return OperationResult<MediaInfoDto>.BadGateway($"{provider.Name} did not answer; try again later");
        }

        // The id is set here rather than left to EF, because the poster file is named by it before
        // the row is saved.
        var info = cached ?? new MediaInfo { Id = Guid.NewGuid(), BasePathId = basePathId, RelativePath = folder };
        Apply(info, parsed, provider, lookup);
        info.HasPoster = await StorePosterAsync(info.Id, lookup?.PosterUrl, cancellationToken);

        if (cached is null)
        {
            m_mediaInfoRepository.Add(info);
        }

        await m_mediaInfoRepository.SaveChangesAsync();

        if (lookup is null)
        {
            m_logger.LogWarning("{Actor:l} looked up \"{Folder:l}\" on {Source:l}: nothing matches \"{Query:l}\"",
                m_auditActor.Describe(), folder, provider.Name, parsed.Title);
        }
        else
        {
            m_logger.LogInformation("{Actor:l} looked up \"{Folder:l}\" on {Source:l}: {Title:l} ({Year})",
                m_auditActor.Describe(), folder, provider.Name, lookup.Title, lookup.Year);
        }

        return OperationResult<MediaInfoDto>.Success(Map(info));
    }

    public async Task<OperationResult<string>> GetPosterPathAsync(Guid userId, bool callerIsAdmin, Guid mediaInfoId)
    {
        var info = await m_mediaInfoRepository.GetAsync(mediaInfoId);
        if (info is null)
        {
            return OperationResult<string>.NotFound("Poster not found");
        }

        // The poster is a fact about a folder on a disk, so it is answered on the disk's own terms.
        var basePath = await m_basePathRepository.GetForUserAsync(info.BasePathId, userId, callerIsAdmin);
        if (basePath is null)
        {
            return NotGranted<string>(userId, info.BasePathId);
        }

        if (!info.HasPoster || !m_posterStore.Exists(info.Id))
        {
            return OperationResult<string>.NotFound("Poster not found");
        }

        return OperationResult<string>.Success(m_posterStore.PathOf(info.Id));
    }

    private static void Apply(MediaInfo info, ParsedTitle parsed, IMediaLookupProvider provider, MediaLookup? lookup)
    {
        info.Query = parsed.Title;
        info.Source = provider.Source;
        info.FetchedAt = DateTime.UtcNow;
        info.Found = lookup is not null;
        info.MediaType = lookup?.MediaType ?? string.Empty;
        info.ExternalId = lookup?.ExternalId ?? 0;
        info.ExternalUrl = lookup?.ExternalUrl ?? string.Empty;
        info.Title = lookup?.Title ?? string.Empty;
        info.Year = lookup?.Year;
        info.Overview = lookup?.Overview ?? string.Empty;
        info.Rating = lookup?.Rating;
        info.Genres = string.Join(", ", lookup?.Genres ?? []);
        info.Platforms = string.Join(", ", lookup?.Platforms ?? []);
    }

    /// <summary>Downloads the poster once and keeps it; a poster that cannot be fetched is a blank,
    /// never a failed look-up.</summary>
    private async Task<bool> StorePosterAsync(Guid id, string? url, CancellationToken cancellationToken)
    {
        if (url is null)
        {
            m_posterStore.Delete(id);
            return false;
        }

        try
        {
            var bytes = await m_posterFetcher.FetchAsync(url, cancellationToken);
            if (bytes is null)
            {
                m_posterStore.Delete(id);
                return false;
            }

            await m_posterStore.SaveAsync(id, bytes, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException)
        {
            m_logger.LogDebug(exception, "Could not fetch the poster {Url:l}", url);
            return m_posterStore.Exists(id);
        }
    }

    private static MediaInfoDto Map(MediaInfo info) => new()
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

    private OperationResult<T> NotGranted<T>(Guid userId, Guid basePathId)
    {
        m_logger.LogWarning(
            "{Actor:l} (user {UserId}) has no access to base path {BasePathId}",
            m_auditActor.Describe(), userId, basePathId);
        return OperationResult<T>.NotFound("Base path not found");
    }
}
