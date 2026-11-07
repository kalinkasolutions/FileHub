using Microsoft.Extensions.Logging;
using Shared;

namespace FileHub.BusinessLogic.Auditing;

/// <summary>
/// The refusal every service answers a base path the caller cannot reach with — no grant of their
/// own, no group that holds it, and not an admin. It answers exactly like a base path that does not
/// exist, so the response cannot be used to enumerate what other users can see, and the log line is
/// the same wherever it is written, so an operator filters for one message.
/// </summary>
public static class NotGranted
{
    public static OperationResult<T> BasePath<T>(ILogger logger, IAuditActor actor, Guid userId, Guid basePathId)
    {
        logger.LogWarning(
            "{Actor:l} (user {UserId}) has no access to base path {BasePathId}",
            actor.Describe(), userId, basePathId);
        return OperationResult<T>.NotFound("Base path not found");
    }
}
