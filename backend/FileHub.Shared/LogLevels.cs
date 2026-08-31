namespace Shared;

/// <summary>
/// The Serilog level names, in order, as they are spelled in the <c>Logs</c> table.
/// <para>
/// This is a plain string list and not a reference to Serilog's <c>LogEventLevel</c> on purpose:
/// <c>FileHub.Shared</c> is referenced by every layer and has no package dependencies, and what is
/// actually needed here is the vocabulary the <i>column</i> uses. The sink writes the enum's name,
/// so the two agree by construction.
/// </para>
/// <para>
/// Note the Serilog spelling, which is not the Microsoft one: the bottom level is <c>Verbose</c>
/// (not Trace) and the top is <c>Fatal</c> (not Critical). <c>Program.ParseLogLevel</c> is where the
/// Microsoft names in configuration are translated across.
/// </para>
/// </summary>
public static class LogLevels
{
    public const string Verbose = "Verbose";
    public const string Debug = "Debug";
    public const string Information = "Information";
    public const string Warning = "Warning";
    public const string Error = "Error";
    public const string Fatal = "Fatal";

    /// <summary>Least to most severe. The order is the whole point of this file.</summary>
    public static readonly string[] All = [Verbose, Debug, Information, Warning, Error, Fatal];

    /// <summary>
    /// The level names at or above <paramref name="minimum"/>, for a <c>WHERE Level IN (...)</c>.
    /// <para>
    /// A set of names rather than a comparison, because the column holds the name and not the rank:
    /// <c>Level &gt;= 'Warning'</c> in SQL would compare strings alphabetically, which puts Debug
    /// above Warning and Error below it — quietly wrong, and wrong in the direction that hides
    /// errors.
    /// </para>
    /// <para>
    /// Returns null for a name that is not a level, which callers read as "do not filter". An
    /// unrecognised level must not narrow the answer to nothing: a log screen that silently shows
    /// zero rows reads as "nothing happened".
    /// </para>
    /// </summary>
    public static string[]? AtOrAbove(string? minimum)
    {
        if (string.IsNullOrWhiteSpace(minimum))
        {
            return null;
        }

        var index = Array.FindIndex(All, x => string.Equals(x, minimum.Trim(), StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            return null;
        }

        return All[index..];
    }
}
