using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Shared;

namespace FileHub.Realtime;

/// <summary>
/// Tells the admin log screen that something has been logged, so it can ask for the new lines
/// instead of asking every couple of seconds whether there are any.
///
/// <para>
/// <b>The hub carries a signal, not the log.</b> It sends one parameterless <c>logged</c> message
/// and the client answers it with an ordinary <c>GET api/admin/logs?afterId=…</c>. Pushing the
/// entries themselves would mean two things this avoids: every connected admin has their own filter
/// (level, text, date range), so the server would have to evaluate each one in memory — a second
/// implementation of the query that can drift from the SQL one — and a <c>LogEvent</c> has no
/// database id yet, because the id is assigned by the SQLite sink's INSERT, so the client would
/// lose the cursor it uses to catch up after a reconnect.
/// </para>
///
/// <para>
/// So: the push decides <i>when</i>, the existing endpoint decides <i>what</i>. There is one
/// filter implementation and one source of ids, and a client that misses a signal is still correct
/// — it just finds the lines on the next one.
/// </para>
///
/// <para>
/// Admin-only, like the endpoint it belongs to. There are no client-callable methods: this is a
/// one-way channel, and a hub method is a route like any other.
/// </para>
/// </summary>
[Authorize(Roles = Roles.Admin)]
public sealed class LogHub : Hub
{
    /// <summary>The message clients listen for. One name, spelled once.</summary>
    public const string LoggedMessage = "logged";
}
