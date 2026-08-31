namespace Shared;

/// <summary>
/// The routes the admin log screen uses to read the log — the query endpoint, the level list, and
/// the hub it is pushed over.
///
/// <para>
/// This exists so that one rule can be stated once and applied in two places, both of which are
/// about the same thing: <b>reading the log must not be an event in the log.</b>
/// </para>
/// <list type="bullet">
/// <item><c>Program.GetRequestLogLevel</c> drops these to Verbose, so an admin leaving the screen
/// open does not fill a table that has no retention with the record of them watching it.</item>
/// <item><c>LogSignalSink</c> refuses to ring for them, which is the load-bearing one: without it
/// the live view feeds itself — the screen fetches, the fetch is request-logged, the entry rings,
/// the ring pushes, the screen fetches. An idle screen made about five requests a second that way,
/// which is worse than the polling the push replaced.</item>
/// </list>
/// <para>
/// Both are needed. The level alone is not enough, because the level is configurable: on an install
/// running at Debug the entry survives and the loop comes back.
/// </para>
/// </summary>
public static class LogRoutes
{
    /// <summary>Everything the log screen calls sits under this.</summary>
    public const string Prefix = "/api/admin/logs";

    /// <summary>
    /// Whether a request path is the log screen reading the log. A null or empty path — an entry
    /// that is not about a request at all, such as a service's own audit line — is not.
    /// </summary>
    public static bool IsLogScreenTraffic(string? requestPath)
    {
        if (string.IsNullOrEmpty(requestPath))
        {
            return false;
        }

        return requestPath.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
    }
}
