using Microsoft.Extensions.Logging;

namespace FileHub.IntegrationTests;

/// <summary>
/// Keeps every line that was logged, so a test can assert on what did <em>not</em> go into it. In
/// production these lines end up in the <c>Logs</c> table in the application's own database, which
/// is why "was this value logged" is worth pinning.
/// </summary>
public sealed class RecordingLogger : ILogger
{
    public List<string> Messages { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => new NoScope();

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        ArgumentNullException.ThrowIfNull(formatter);

        Messages.Add(formatter(state, exception));

        // The structured arguments too: Serilog persists those as their own column, so a value that
        // never appears in the rendered message is just as stored.
        if (state is IEnumerable<KeyValuePair<string, object?>> properties)
        {
            Messages.AddRange(properties.Select(p => $"{p.Key}={p.Value}"));
        }
    }

    private sealed class NoScope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
