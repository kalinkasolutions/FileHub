using FileHub.BusinessLogic.Services.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FileHub.BusinessLogic.Media;

/// <summary>
/// Runs the look-ups share link previews queued, one at a time, after the preview has already been
/// answered. A chat client gives up on an unfurl after a few seconds, so it never waits on a source.
/// </summary>
public sealed class MediaRefreshWorker : BackgroundService
{
    private readonly MediaRefreshQueue m_queue;
    private readonly IServiceScopeFactory m_scopeFactory;
    private readonly ILogger<MediaRefreshWorker> m_logger;

    public MediaRefreshWorker(MediaRefreshQueue queue, IServiceScopeFactory scopeFactory, ILogger<MediaRefreshWorker> logger)
    {
        m_queue = queue;
        m_scopeFactory = scopeFactory;
        m_logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            MediaRefreshRequest request;

            try
            {
                request = await m_queue.ReadAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            await RefreshAsync(request, stoppingToken);
        }
    }

    /// <summary>One queued look-up, in a scope of its own: the request that queued it is long gone.</summary>
    public async Task RefreshAsync(MediaRefreshRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = m_scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IMediaInfoService>();
            await service.RefreshForLinkAsync(request.BasePathId, request.Folder, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown, not a failure.
        }
        catch (Exception exception)
        {
            // The loop must outlive any one folder, or every later preview stays stale.
            m_logger.LogWarning(exception, "Could not refresh \"{Folder:l}\" for a link preview", request.Folder);
        }
        finally
        {
            m_queue.Done(request);
        }
    }
}
