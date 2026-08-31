using Microsoft.AspNetCore.SignalR;
using Shared;

namespace FileHub.Realtime;

/// <summary>
/// Turns rings of <see cref="LogChangeSignal"/> into <c>logged</c> messages on <see cref="LogHub"/>.
///
/// <para>
/// This is the only thing that broadcasts, and it is deliberately not the sink: doing it on the
/// logging thread would put a network send inside every <c>ILogger</c> call, and sending itself
/// logs — see the note on <see cref="LogChangeSignal"/> for why that has to be broken apart.
/// </para>
/// </summary>
public sealed class LogBroadcastService : BackgroundService
{
    /// <summary>
    /// How long to keep quiet after a broadcast, so a burst becomes one notification rather than a
    /// stream of them. Anything logged during the pause refills the signal's single slot and is
    /// picked up on the next turn, so nothing is lost — it is only ever delivered later, and the
    /// client re-reads by id, so "later" costs nothing but the delay.
    ///
    /// <para>
    /// Short enough to read as live, long enough that a request that logs a dozen lines is one
    /// round trip to the browser and not a dozen.
    /// </para>
    /// </summary>
    private static readonly TimeSpan CoalesceWindow = TimeSpan.FromMilliseconds(200);

    private readonly LogChangeSignal m_signal;
    private readonly IHubContext<LogHub> m_hubContext;
    private readonly ILogger<LogBroadcastService> m_logger;

    public LogBroadcastService(
        LogChangeSignal signal,
        IHubContext<LogHub> hubContext,
        ILogger<LogBroadcastService> logger)
    {
        m_signal = signal;
        m_hubContext = hubContext;
        m_logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await m_signal.WaitAsync(stoppingToken);
                await m_hubContext.Clients.All.SendAsync(LogHub.LoggedMessage, stoppingToken);
                await Task.Delay(CoalesceWindow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutdown, not a failure.
                return;
            }
            catch (Exception exception)
            {
                // Never let this loop die: it is the only thing driving the live log, and a screen
                // that has silently stopped updating is worse than one that says it is offline.
                // Logging here rings the signal again, which is harmless — the slot is one deep and
                // the pause below keeps it from spinning.
                m_logger.LogWarning(exception, "Could not notify the admin log screens of new entries");
                await Task.Delay(CoalesceWindow, CancellationToken.None);
            }
        }
    }
}
