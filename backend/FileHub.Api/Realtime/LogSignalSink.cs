using Serilog.Core;
using Serilog.Events;
using Shared;

namespace FileHub.Realtime;

/// <summary>
/// A Serilog sink whose only job is to ring <see cref="LogChangeSignal"/>. It writes nothing and
/// keeps nothing.
///
/// <para>
/// It sits in the pipeline beside the console and SQLite sinks, which is what makes the admin log
/// screen event-driven: the screen is told the moment a line is written rather than asking every
/// couple of seconds whether one was.
/// </para>
///
/// <para>
/// <see cref="Emit"/> is on the hot path of every single log call in the application, so it does
/// almost nothing: one property lookup and a non-blocking, non-throwing enqueue. Deciding whether
/// anyone is listening, serialising and sending all belong to the background reader.
/// </para>
/// </summary>
public sealed class LogSignalSink : ILogEventSink
{
    /// <summary>The property <c>UseSerilogRequestLogging</c> puts the request's path in.</summary>
    private const string RequestPathProperty = "RequestPath";

    private readonly LogChangeSignal m_signal;

    public LogSignalSink(LogChangeSignal signal)
    {
        m_signal = signal;
    }

    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        // Reading the log must not ring the bell, or the live view feeds itself — see LogRoutes.
        if (LogRoutes.IsLogScreenTraffic(ReadRequestPath(logEvent)))
        {
            return;
        }

        m_signal.Ring();
    }

    /// <summary>
    /// The request path an entry belongs to, or null when it is not about a request — a service's
    /// own audit line, for instance.
    /// </summary>
    private static string? ReadRequestPath(LogEvent logEvent)
    {
        if (!logEvent.Properties.TryGetValue(RequestPathProperty, out var property))
        {
            return null;
        }

        // Read the value rather than rendering it: ToString() on a ScalarValue wraps a string in
        // quotes, which no prefix comparison would match.
        return property is ScalarValue { Value: string path } ? path : null;
    }
}
