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
        // Each refresh is a request to a third party. The route policy says the same; this is where
        // the rule is visible beside the logic and settable from a service-level test.
        if (refresh && !callerIsAdmin)
        {
            return OperationResult<MediaInfoDto>.Forbidden("Only an admin may look a title up again");
        }

        var basePath = await m_basePathRepository.GetForUserAsync(basePathId, userId, callerIsAdmin);
        if (basePath is null)
        {
            return NotGranted.BasePath<MediaInfoDto>(m_logger, m_auditActor, userId, basePathId);
        }

        if (basePath.Kind == BasePathKind.None)
        {
            return OperationResult<MediaInfoDto>.BadRequest("This disk is not marked as media or games, so there is nothing to look up");
        }

        var folder = relativePath ?? string.Empty;

        if (!IsTitleFolder(folder))
        {
            return OperationResult<MediaInfoDto>.BadRequest("Only a folder directly under the disk has info");
        }

        if (!PathSandbox.TryResolve(basePath.Path, folder, out var fullPath) || !Directory.Exists(fullPath))
        {
            return OperationResult<MediaInfoDto>.NotFound("Path not found");
        }

        var cached = await m_mediaInfoRepository.GetAsync(basePathId, folder);
        if (cached is not null && !refresh && !MediaInfoAge.IsStale(cached, DateTime.UtcNow))
        {
            return OperationResult<MediaInfoDto>.Success(MediaInfoMapping.ToDto(cached));
        }

        return await FetchAsync(basePath, folder, cached, cancellationToken);
    }

    public async Task RefreshForLinkAsync(Guid basePathId, string folder, CancellationToken cancellationToken)
    {
        // By id alone, because there is no caller: only a share link that resolved queues this, and
        // losing access to a base path deletes the links into it.
        var basePath = await m_basePathRepository.GetAsync(basePathId);
        if (basePath is null || basePath.Kind == BasePathKind.None || !IsTitleFolder(folder))
        {
            return;
        }

        if (!PathSandbox.TryResolve(basePath.Path, folder, out var fullPath) || !Directory.Exists(fullPath))
        {
            return;
        }

        // Asked again here, not only when queued: another look-up may have answered in between.
        var cached = await m_mediaInfoRepository.GetAsync(basePathId, folder);
        if (!MediaInfoAge.NeedsRefreshForLink(cached, DateTime.UtcNow))
        {
            return;
        }

        await FetchAsync(basePath, folder, cached, cancellationToken);
    }

    /// <summary>Asks the source and stores the answer over <paramref name="cached"/>, or as a new row.</summary>
    private async Task<OperationResult<MediaInfoDto>> FetchAsync(
        BasePath basePath, string folder, MediaInfo? cached, CancellationToken cancellationToken)
    {
        var basePathId = basePath.Id;
        var provider = m_providers.FirstOrDefault(p => p.Kind == basePath.Kind)
                       ?? throw new InvalidOperationException($"No look-up provider is registered for {basePath.Kind}");

        if (!provider.IsConfigured)
        {
            return StaleOrFailed(cached, $"{provider.Name} is not configured: {provider.NotConfiguredHint}", OperationResult<MediaInfoDto>.BadRequest);
        }

        var parsed = FolderTitleParser.Parse(folder);
        MediaLookup? lookup;

        try
        {
            lookup = await provider.LookupAsync(parsed, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller went away; nobody reads this answer and nothing went wrong.
            return OperationResult<MediaInfoDto>.BadGateway($"{provider.Name} was not waited for");
        }
        catch (MediaLookupException exception)
        {
            m_logger.LogWarning(exception, "{Actor:l} looked up \"{Folder:l}\" on {Source:l}: {Reason:l}",
                m_auditActor.Describe(), folder, provider.Name, exception.Message);
            return StaleOrFailed(cached, exception.Message, OperationResult<MediaInfoDto>.BadGateway);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException)
        {
            // Not cached: the next ask tries again, which is what a network blip wants.
            m_logger.LogWarning(exception, "{Actor:l} looked up \"{Folder:l}\" on {Source:l}, which did not answer",
                m_auditActor.Describe(), folder, provider.Name);
            return StaleOrFailed(cached, $"{provider.Name} did not answer; try again later", OperationResult<MediaInfoDto>.BadGateway);
        }

        // The id is set here rather than left to EF, because the poster file is named by it before
        // the row is saved.
        var info = cached ?? new MediaInfo { Id = Guid.NewGuid(), BasePathId = basePathId, RelativePath = folder };
        Apply(info, parsed, provider, lookup);
        info.HasPoster = await StorePosterAsync(info.Id, lookup?.PosterUrl, cancellationToken);

        if (cached is not null)
        {
            await m_mediaInfoRepository.SaveChangesAsync();
        }
        else if (!await m_mediaInfoRepository.TryAddAsync(info))
        {
            // Somebody else looked this folder up while the source was answering. Their row is the
            // answer, and the poster just saved under this id belongs to no row.
            m_posterStore.Delete(info.Id);
            var theirs = await m_mediaInfoRepository.GetAsync(basePathId, folder);

            return theirs is null
                ? OperationResult<MediaInfoDto>.NotFound("Path not found")
                : OperationResult<MediaInfoDto>.Success(MediaInfoMapping.ToDto(theirs));
        }

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

        return OperationResult<MediaInfoDto>.Success(MediaInfoMapping.ToDto(info));
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
            return NotGranted.BasePath<string>(m_logger, m_auditActor, userId, info.BasePathId);
        }

        if (!info.HasPoster || !m_posterStore.Exists(info.Id))
        {
            return OperationResult<string>.NotFound("Poster not found");
        }

        return OperationResult<string>.Success(m_posterStore.PathOf(info.Id));
    }

    /// <summary>Only the folders directly under the base path are titles; "season_01" two levels
    /// down is not, and neither is the base path itself.</summary>
    private static bool IsTitleFolder(string folder) =>
        folder.Length > 0 && !folder.Contains('/') && !folder.Contains('\\');

    /// <summary>A stale answer is still an answer; only a first look-up has nothing to fall back on.</summary>
    private static OperationResult<MediaInfoDto> StaleOrFailed(
        MediaInfo? cached, string message, Func<string, OperationResult<MediaInfoDto>> failure) =>
        cached is not null ? OperationResult<MediaInfoDto>.Success(MediaInfoMapping.ToDto(cached)) : failure(message);

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
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or IOException)
        {
            m_logger.LogDebug(exception, "Could not fetch the poster {Url:l}", url);
            return m_posterStore.Exists(id);
        }
    }
}
