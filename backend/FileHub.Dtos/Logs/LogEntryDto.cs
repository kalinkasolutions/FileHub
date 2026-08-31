namespace Dtos.Logs;

/// <summary>
/// One line of the log as the admin screen shows it. The sink's raw <c>Properties</c> JSON is
/// deliberately not carried: it repeats what the rendered message already says, and it is where
/// Serilog puts every argument a call site passed — including ones nobody meant to publish.
/// </summary>
public sealed class LogEntryDto
{
    /// <summary>
    /// The sink's row id. The client sends the highest one it holds back as
    /// <c>LogQueryDto.AfterId</c> to tail the log, so it is a cursor and not decoration.
    /// </summary>
    public long Id { get; set; }

    /// <summary>UTC. The client renders it in the viewer's own zone.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>A Serilog level name: Verbose, Debug, Information, Warning, Error or Fatal.</summary>
    public string Level { get; set; }

    public string Message { get; set; }

    /// <summary>The exception's full text when the entry carried one, else null.</summary>
    public string Exception { get; set; }
}
