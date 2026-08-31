namespace FileHub.BusinessLogic.Auditing;

/// <summary>
/// The actor for work that no request asked for: startup seeding, a background send, and the
/// service-level tests, which have no <c>HttpContext</c> to read a principal from.
///
/// <para>
/// It exists so that <see cref="IAuditActor"/> always has a registration. A missing one would make
/// every service that writes an audit line fail to resolve, which turns "the log is less
/// informative here" into "this whole slice does not start".
/// </para>
/// </summary>
public sealed class SystemAuditActor : IAuditActor
{
    public const string Description = "system";

    public string Describe() => Description;
}
