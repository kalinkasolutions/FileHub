using Dal.Repositories.Logs;
using FileHub.BusinessLogic.Services.Logs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FileHub.IntegrationTests;

/// <summary>
/// Fixture for the admin log viewer.
///
/// <para>
/// The <c>Logs</c> table belongs to the Serilog SQLite sink, and the EF model marks it
/// <c>ExcludeFromMigrations</c> — which <c>EnsureCreated</c> honours too, so the fixture's database
/// comes up without it. This creates it with the sink's exact schema, which is also what pins that
/// schema: if the sink's shape ever changes under an upgrade, the mapping and this DDL disagree and
/// these tests are where that shows up.
/// </para>
/// </summary>
public abstract class LogTestBase : TestHostBase
{
    protected ILogService Logs { get; }

    protected LogTestBase() : base(services =>
    {
        services.AddScoped<ILogRepository, LogRepository>();
        services.AddScoped<ILogService, LogService>();
    })
    {
        Logs = Services.GetRequiredService<ILogService>();

        // Verbatim from Serilog.Sinks.SQLite. Note `id` in lower case and the untyped TEXT columns:
        // they are the sink's conventions, not this project's.
        Context.Database.ExecuteSqlRaw(
            "CREATE TABLE IF NOT EXISTS Logs (id INTEGER PRIMARY KEY AUTOINCREMENT, Timestamp TEXT, "
            + "Level VARCHAR(10), Exception TEXT, RenderedMessage TEXT, Properties TEXT)");
    }

    /// <summary>
    /// Writes one row the way the sink would: an ISO-8601 UTC timestamp with a 'T' separator and
    /// milliseconds. Inserted as raw SQL rather than through EF on purpose — the point of these
    /// tests is that our queries read what the *sink* writes, so the test must not go through the
    /// same mapping it is checking.
    /// </summary>
    protected void WriteLog(DateTime timestampUtc, string level, string message, string? exception = null)
    {
        Context.Database.ExecuteSqlRaw(
            "INSERT INTO Logs (Timestamp, Level, Exception, RenderedMessage, Properties) VALUES ({0}, {1}, {2}, {3}, {4})",
            timestampUtc.ToString("yyyy-MM-ddTHH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture),
            level,
            exception,
            message,
            "{}");
    }
}
