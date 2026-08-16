using System.Globalization;
using System.Security.Claims;
using Dtos.Shares;
using FileHub.BusinessLogic;
using FileHub.BusinessLogic.Services.Shares;
using FileHub.Downloads;
using FileHub.Extensions;
using FileHub.Links;
using Microsoft.Extensions.Options;
using Shared;

namespace FileHub.Endpoints;

/// <summary>
/// The three routes the internet reaches without a cookie. They are deliberately the whole
/// anonymous surface, and deliberately thin: no user lookup, no directory walk, nothing but the
/// share row, one sandbox resolution and a stat.
/// <para>
/// They stay <c>AllowAnonymous</c>, but they now read the principal: a link aimed at a group only
/// answers a signed-in member of it. <c>UseAuthentication()</c> runs before them, so the cookie is
/// already decoded when there is one — an anonymous caller costs nothing extra, and a link with no
/// audience costs nothing extra either.
/// </para>
/// </summary>
public static class PublicShareEndpoint
{
    public static void MapPublicShareEndpoint(this IEndpointRouteBuilder builder)
    {
        // Rate limited because they are anonymous: nothing else stands between a leaked link and
        // however many zip rebuilds an attacker cares to ask for. The policy lives in Program.cs
        // and needs app.UseRateLimiter() in the pipeline — the metadata alone does nothing.
        var group = builder.MapGroup("public-api/share").AllowAnonymous().RequireRateLimiting("public");

        group.MapGet("{id:guid}", GetAsync);
        group.MapGet("{id:guid}/download", DownloadAsync);

        // Not under public-api: this one is a page, not an API call, and it is the URL a share link
        // actually points at so that a chat client unfurling it finds Open Graph tags.
        builder.MapGet("og/share/{id:guid}", OpenGraphAsync).AllowAnonymous().RequireRateLimiting("public");
    }

    private static async Task<IResult> GetAsync(Guid id, ClaimsPrincipal user, IShareService service)
    {
        var result = await service.ResolvePublicAsync(id, CallerId(user), user.IsInRole(Roles.Admin));

        if (result.HasError)
        {
            return result.MapError<PublicShareDto>().ToHttpResult();
        }

        var share = result.Value;

        // Projected here rather than returned by the service, because ResolvedShare carries the
        // absolute path on the host and this is the one response an anonymous caller sees.
        var dto = new PublicShareDto
        {
            Id = share.Id,
            Name = share.Name,
            Size = share.Size,
            IsDir = share.IsDirectory
        };

        return Results.Ok(dto);
    }

    private static async Task<IResult> DownloadAsync(
        Guid id,
        ClaimsPrincipal user,
        IShareService service,
        IOptions<AppOptions> options,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        var callerId = CallerId(user);
        var callerIsAdmin = user.IsInRole(Roles.Admin);

        var result = await service.ResolvePublicAsync(id, callerId, callerIsAdmin);

        if (result.HasError)
        {
            // A browser follows this link directly, so a dead, exhausted or not-for-you link is
            // answered with the app's 404 page rather than with a ProblemDetails body it would
            // render as raw JSON. All three look the same on purpose.
            return Results.Redirect(ShareLinks.NotFound(options.Value));
        }

        var share = result.Value;

        // Counted before the first byte: a client that disconnects halfway still consumed the link,
        // and counting afterwards would let a limit be evaded by aborting every download.
        //
        // The count is also what *decides*, and so is the audience. The resolve above read the
        // counter and the increment writes it, and between those two statements any number of
        // concurrent callers read the same value — a link capped at one download served eight of
        // them. So the claim is a single conditional UPDATE and its affected-row count says whether
        // this caller got the last one.
        var registered = await service.RegisterDownloadAsync(id, callerId, callerIsAdmin);

        if (registered.HasError)
        {
            return Results.Redirect(ShareLinks.NotFound(options.Value));
        }

        return FileDownload.Create(
            context, share.FullPath, share.Name, share.IsDirectory,
            loggerFactory.CreateLogger(nameof(PublicShareEndpoint)));
    }

    private static async Task<IResult> OpenGraphAsync(
        Guid id, ClaimsPrincipal user, IShareService service, IOptions<AppOptions> options)
    {
        var result = await service.ResolvePublicAsync(id, CallerId(user), user.IsInRole(Roles.Admin));

        // A dead link still renders a page rather than a 404: this URL is what gets pasted into a
        // chat, and the crawler that fetches it should get tags, not an error. A link aimed at a
        // group renders the same generic page for anyone outside it — the crawler is never signed
        // in, so leaking the file name here would defeat the audience for every link ever pasted.
        var title = result.IsSuccess ? result.Value.Name : "not available";
        var size = result.IsSuccess ? OpenGraphPage.FormatSize(result.Value.Size) : string.Empty;

        var page = OpenGraphPage.Render(
            title,
            string.Create(CultureInfo.InvariantCulture, $"{title}, {size}"),
            ShareLinks.PreviewImage(options.Value),
            ShareLinks.Share(options.Value, id),
            LandingPath(id));

        return Results.Content(page, "text/html");
    }

    /// <summary>The caller's id, or null when nobody is signed in — which is the ordinary case here.</summary>
    private static Guid? CallerId(ClaimsPrincipal user) => user.TryGetUserId(out var id) ? id : null;

    /// <summary>Relative on purpose: the redirect stays on whatever host served the page, so a link
    /// opened through an alternative hostname does not bounce the visitor to the configured one.</summary>
    private static string LandingPath(Guid id) => $"/share/{id}";
}
