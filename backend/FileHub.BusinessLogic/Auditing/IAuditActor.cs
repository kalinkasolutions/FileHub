namespace FileHub.BusinessLogic.Auditing;

/// <summary>
/// Who is making the current request, as a phrase to put in a log message.
///
/// <para>
/// <b>This is for log messages and nothing else.</b> It is never consulted to decide whether
/// something is allowed. Every authorization input in this codebase is threaded from the endpoint
/// as an ordinary argument — <c>callerIsAdmin</c>, <c>callerCanCreateShares</c>, <c>callerId</c> —
/// precisely so that a reader can see what decides an answer and a service-level test can set it.
/// That rule is unchanged. Resolving the <i>actor's name</i> ambiently is a different thing: it
/// changes no answer, and threading a display name through twenty signatures that have no other
/// use for it would obscure the ones that do.
/// </para>
///
/// <para>
/// It never throws and never returns null: an audit line is written on paths where there may be no
/// principal at all (the anonymous share routes, a background task), and a log call is not allowed
/// to be the thing that fails a request.
/// </para>
/// </summary>
public interface IAuditActor
{
    /// <summary>
    /// A short phrase naming the caller — <c>Admin &lt;admin@example.com&gt;</c> — or a stand-in
    /// like <c>anonymous</c> when there is no signed-in principal.
    /// </summary>
    string Describe();
}
