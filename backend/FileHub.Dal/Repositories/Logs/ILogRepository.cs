using Entities.Logs;

namespace Dal.Repositories.Logs;

/// <summary>
/// Read-only access to the Serilog sink's <c>Logs</c> table. There is deliberately no write here:
/// the sink owns the table, and anything this application wants to say goes through
/// <c>ILogger</c> like every other line.
/// </summary>
public interface ILogRepository
{
    /// <summary>The matching rows, newest first, capped at <c>LogFilter.Take</c>.</summary>
    Task<List<LogEntry>> QueryAsync(LogFilter filter);

    /// <summary>
    /// How many rows match, ignoring <c>Take</c> and <c>AfterId</c> — the size of the set the page
    /// is a window onto.
    /// </summary>
    Task<int> CountAsync(LogFilter filter);

    /// <summary>
    /// Creates the indexes the filters need, if the sink's table exists yet. Idempotent.
    /// </summary>
    Task EnsureIndexesAsync();
}
