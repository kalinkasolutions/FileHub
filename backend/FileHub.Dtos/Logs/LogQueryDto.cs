using System.ComponentModel.DataAnnotations;

namespace Dtos.Logs;

/// <summary>
/// The filter behind the admin log screen. Every field is optional; the default is "the newest
/// <see cref="Take"/> lines, whatever they are".
/// </summary>
public sealed class LogQueryDto
{
    /// <summary>
    /// The <i>lowest</i> level to include, as a Serilog level name — "Warning" answers warnings,
    /// errors and fatals. Null or empty means every level.
    /// <para>
    /// A minimum rather than an exact match because that is the question an operator actually has
    /// ("show me anything that went wrong"), and because an exact match on "Error" hides the Fatal
    /// that followed it.
    /// </para>
    /// </summary>
    [MaxLength(20)]
    public string MinLevel { get; set; }

    /// <summary>
    /// Case-insensitive substring of the rendered message. Null or empty does not filter.
    /// </summary>
    [MaxLength(200)]
    public string Search { get; set; }

    /// <summary>Inclusive lower bound on the entry's UTC timestamp.</summary>
    public DateTime? From { get; set; }

    /// <summary>Inclusive upper bound on the entry's UTC timestamp.</summary>
    public DateTime? To { get; set; }

    /// <summary>
    /// Return only rows newer than this id. This is how the screen tails the log: it sends back the
    /// highest id it already holds, and gets only what has arrived since.
    /// <para>
    /// An id and not a timestamp, because two entries can share a millisecond and a timestamp
    /// cursor then either repeats one or drops one.
    /// </para>
    /// </summary>
    public long? AfterId { get; set; }

    /// <summary>
    /// How many rows to return, newest first. Capped by the service — this is an unbounded table
    /// and the screen is not a reason to load all of it into memory.
    /// </summary>
    [Range(1, 1000)]
    public int Take { get; set; } = 200;
}
