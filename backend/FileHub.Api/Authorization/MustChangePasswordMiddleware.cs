using Microsoft.AspNetCore.Mvc;

namespace FileHub.Authorization;

/// <summary>
/// Holds a session whose account still carries an admin-set password to one thing: changing it.
/// Everything else answers 403 with <c>type: must-change-password</c>, which is the SPA's cue to
/// show the password screen instead of whatever the user asked for. Must run after
/// <c>UseAuthentication</c> — it reads the claim the sign-in cookie carries.
/// </summary>
public sealed class MustChangePasswordMiddleware
{
    private readonly RequestDelegate m_next;

    public MustChangePasswordMiddleware(RequestDelegate next)
    {
        m_next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await m_next(context);
            return;
        }

        if (!context.User.HasClaim(FileHubClaimsPrincipalFactory.MustChangePasswordClaim, "true"))
        {
            await m_next(context);
            return;
        }

        if (IsAllowed(context.Request))
        {
            await m_next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Type = "must-change-password",
            Title = "Password change required",
            Status = StatusCodes.Status403Forbidden,
            Detail = "Your password was set by an administrator and has to be changed before you can continue."
        }, context.RequestAborted);
    }

    /// <summary>
    /// The routes a gated session may still reach: the password change itself, the two reads the
    /// password screen is built from, the way out, and everything that isn't the restricted API —
    /// the share links under <c>public-api</c>/<c>og</c> and the SPA's own files, none of which
    /// depend on who is signed in.
    /// </summary>
    private static bool IsAllowed(HttpRequest request)
    {
        var path = request.Path;

        if (path.StartsWithSegments("/public-api"))
        {
            return true;
        }

        if (path.StartsWithSegments("/og"))
        {
            return true;
        }

        if (!path.StartsWithSegments("/api"))
        {
            return true;
        }

        if (HttpMethods.IsPost(request.Method) && path.Equals("/api/account/password", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (HttpMethods.IsGet(request.Method) && path.Equals("/api/account", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (HttpMethods.IsGet(request.Method) && path.Equals("/api/auth/status", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (HttpMethods.IsPost(request.Method) && path.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
