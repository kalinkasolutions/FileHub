namespace Dal.Repositories.Logs;

/// <summary>
/// What the log query narrows on, in the terms the <c>Logs</c> table actually stores. The service
/// translates the screen's filter into this — a minimum level becomes the <see cref="Levels"/> it
/// covers, and a <c>DateTime</c> becomes nothing at all, because the bounds stay as
/// <see cref="DateTime"/> here and are formatted to the sink's text format inside the repository
/// (see <c>LogRepository</c>).
/// </summary>
public sealed class LogFilter
{
    /// <summary>The level names to include. Null or empty does not filter.</summary>
    public IReadOnlyCollection<string>? Levels { get; init; }

    /// <summary>Case-insensitive substring of the rendered message. Null or empty does not filter.</summary>
    public string? Search { get; init; }

    /// <summary>Inclusive lower bound, UTC.</summary>
    public DateTime? From { get; init; }

    /// <summary>Inclusive upper bound, UTC.</summary>
    public DateTime? To { get; init; }

    /// <summary>Only rows with a larger id. Null does not filter.</summary>
    public long? AfterId { get; init; }

    /// <summary>How many rows to return, newest first.</summary>
    public int Take { get; init; } = 200;
}
