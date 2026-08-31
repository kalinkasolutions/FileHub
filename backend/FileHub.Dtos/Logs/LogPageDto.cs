namespace Dtos.Logs;

/// <summary>
/// One page of the log, newest first, plus what the client needs to ask for the next thing.
/// </summary>
public sealed class LogPageDto
{
    public LogEntryDto[] Entries { get; set; } = [];

    /// <summary>
    /// How many rows match the filter, ignoring paging. This is a <c>COUNT</c> over the whole
    /// filtered set, which is what lets the screen say "showing 200 of 12,480".
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// True when <see cref="TotalCount"/> is larger than the page returned, so the screen can say
    /// the list is cut off rather than letting an admin believe they are looking at everything.
    /// </summary>
    public bool HasMore { get; set; }
}
