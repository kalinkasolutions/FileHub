namespace Entities.Logs;

/// <summary>
/// One row of the <c>Logs</c> table — the Serilog SQLite sink's own table, living in the same
/// database file as everything else.
/// <para>
/// <b>This table is not ours.</b> The sink creates it and writes to it through its own ADO.NET
/// connection, so the entity is mapped read-only and excluded from migrations (see
/// <c>FileHubContext</c>): EF must never try to create, alter or drop it. The column names and
/// types below are the sink's, which is why they do not follow this project's conventions.
/// </para>
/// <para>
/// <see cref="Timestamp"/> is deliberately a <see cref="string"/> and not a
/// <see cref="DateTime"/>. The sink writes ISO-8601 with a 'T' separator and milliseconds
/// (<c>2026-08-31T14:45:39.027</c>, UTC), while EF's SQLite provider formats a
/// <see cref="DateTime"/> parameter with a space separator and seven decimals — so a range
/// comparison written against a <see cref="DateTime"/> property compares two different formats and
/// silently matches the wrong rows. Kept as text, the format is fixed-width and lexicographic
/// comparison is exact; <c>LogRepository</c> formats its bounds the same way.
/// </para>
/// </summary>
public sealed class LogEntry
{
    /// <summary>The sink's autoincrement key. It is also the tail cursor: ids only ever grow.</summary>
    public long Id { get; set; }

    /// <summary>UTC, as ISO-8601 text — see the note on the class.</summary>
    public string Timestamp { get; set; }

    /// <summary>A Serilog level name: Verbose, Debug, Information, Warning, Error or Fatal.</summary>
    public string Level { get; set; }

    public string Exception { get; set; }

    /// <summary>The message with its properties already substituted in.</summary>
    public string RenderedMessage { get; set; }

    /// <summary>The structured properties, as the JSON object the sink stored.</summary>
    public string Properties { get; set; }
}
