using System.Globalization;
using Entities.Logs;
using Microsoft.EntityFrameworkCore;

namespace Dal.Repositories.Logs;

public sealed class LogRepository : ILogRepository
{
    /// <summary>
    /// How the Serilog SQLite sink writes a timestamp: ISO-8601, 'T' separator, milliseconds, UTC,
    /// no offset suffix. A bound has to be formatted exactly this way or the string comparison the
    /// range filter performs is comparing two different shapes — see the note on
    /// <see cref="LogEntry"/>.
    /// </summary>
    private const string SinkTimestampFormat = "yyyy-MM-ddTHH:mm:ss.fff";

    /// <summary>
    /// The character that turns a literal % or _ in a search term back into itself. Backslash is
    /// not SQLite's default — LIKE has no escape character at all unless one is named — so every
    /// LIKE below carries an explicit ESCAPE clause.
    /// </summary>
    private const string LikeEscape = "\\";

    private readonly FileHubContext m_context;

    public LogRepository(FileHubContext context)
    {
        m_context = context;
    }

    public Task<List<LogEntry>> QueryAsync(LogFilter filter)
    {
        var query = Apply(m_context.Logs.AsNoTracking(), filter);

        // AfterId is the tail cursor, so it narrows the page but must not narrow the count: "12
        // new lines out of 4,000 matching" is two different questions.
        if (filter.AfterId.HasValue)
        {
            query = query.Where(x => x.Id > filter.AfterId.Value);
        }

        // Newest first, and by id rather than by timestamp: ids are assigned in write order and are
        // unique, so the order is total. Two entries inside the same millisecond share a timestamp,
        // and an unstable sort there makes paging repeat or skip a row.
        return query
            .OrderByDescending(x => x.Id)
            .Take(filter.Take)
            .ToListAsync();
    }

    public Task<int> CountAsync(LogFilter filter) =>
        Apply(m_context.Logs.AsNoTracking(), filter).CountAsync();

    public async Task EnsureIndexesAsync()
    {
        // The sink owns this table and creates it on its own schedule, so this cannot assume it is
        // there. It is by the time the host has built a logger, but a guard is cheaper than a
        // startup that falls over on "no such table: Logs".
        var exists = await m_context.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'Logs'")
            .SingleAsync();

        if (exists == 0)
        {
            return;
        }

        // The sink creates the table with nothing but its INTEGER PRIMARY KEY, which is the rowid —
        // so "newest first" is already free, and these two are what the screen's own filters need.
        // Without them every level or date filter is a full scan of a table that has no retention
        // and grows for the life of the install.
        //
        // Raw DDL because the table is excluded from migrations: it is not ours to describe in the
        // model, but the indexes on it are ours to keep.
        await m_context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_Logs_Timestamp ON Logs (Timestamp)");
        await m_context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_Logs_Level ON Logs (Level)");
    }

    /// <summary>
    /// The filter both the page and the count share, so the two can never drift into answering
    /// about different sets.
    /// </summary>
    private static IQueryable<LogEntry> Apply(IQueryable<LogEntry> query, LogFilter filter)
    {
        if (filter.Levels is { Count: > 0 })
        {
            var levels = filter.Levels.ToArray();
            query = query.Where(x => x.Level != null && levels.Contains(x.Level));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // LIKE rather than Contains: EF translates Contains to instr(), which is case-sensitive,
            // and a log search that misses "Deleted" because the box said "deleted" is not a search.
            // SQLite's LIKE is case-insensitive for ASCII, which is what a log message is.
            var pattern = $"%{EscapeLike(filter.Search.Trim())}%";
            query = query.Where(x =>
                x.RenderedMessage != null && EF.Functions.Like(x.RenderedMessage, pattern, LikeEscape));
        }

        // Text comparison, against bounds formatted the way the sink writes them. The format is
        // fixed-width, so lexicographic order is chronological order.
        if (filter.From.HasValue)
        {
            var from = Format(filter.From.Value);
            query = query.Where(x => x.Timestamp != null && string.Compare(x.Timestamp, from) >= 0);
        }

        if (filter.To.HasValue)
        {
            var to = Format(filter.To.Value);
            query = query.Where(x => x.Timestamp != null && string.Compare(x.Timestamp, to) <= 0);
        }

        return query;
    }

    private static string Format(DateTime value) =>
        value.ToUniversalTime().ToString(SinkTimestampFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// Makes a user's search term a literal. Without this a term containing % matches everything and
    /// a term containing _ matches any character — the filter would quietly answer the wrong question
    /// for any message with a path or an identifier in it.
    /// </summary>
    private static string EscapeLike(string value) =>
        value
            .Replace(LikeEscape, LikeEscape + LikeEscape, StringComparison.Ordinal)
            .Replace("%", LikeEscape + "%", StringComparison.Ordinal)
            .Replace("_", LikeEscape + "_", StringComparison.Ordinal);
}
