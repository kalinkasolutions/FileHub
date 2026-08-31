using System.Security.Claims;
using FileHub.BusinessLogic.Auditing;

namespace FileHub.Auditing;

/// <summary>
/// Names the caller of the current request from the sign-in cookie's own claims, so an audit line
/// costs no database read.
/// </summary>
public sealed class HttpContextAuditActor : IAuditActor
{
    /// <summary>What an unauthenticated caller is called — the public share routes have one.</summary>
    public const string Anonymous = "anonymous";

    private readonly IHttpContextAccessor m_httpContextAccessor;

    public HttpContextAuditActor(IHttpContextAccessor httpContextAccessor)
    {
        m_httpContextAccessor = httpContextAccessor;
    }

    public string Describe()
    {
        var user = m_httpContextAccessor.HttpContext?.User;

        if (user?.Identity is null || !user.Identity.IsAuthenticated)
        {
            return Anonymous;
        }

        var name = user.FindFirstValue(ClaimTypes.Name);
        var email = user.FindFirstValue(ClaimTypes.Email);

        // Both, when there are both. The display name is what a reader recognises, but it is not
        // unique and can be changed by its owner — the address is the account's actual identity
        // (see UserName in CLAUDE.md), so an audit line that only carried the name could name two
        // different accounts identically.
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(email))
        {
            return $"{name} <{email}>";
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            return email;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        // Authenticated but carrying neither claim: the id is still better than nothing, and this
        // is the branch that shows up if the claims factory ever stops emitting one of them.
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);

        return string.IsNullOrWhiteSpace(id) ? Anonymous : id;
    }
}
