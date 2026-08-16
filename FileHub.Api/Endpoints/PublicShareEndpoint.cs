using System.Globalization;
using Dtos.Shares;
using FileHub.BusinessLogic;
using FileHub.BusinessLogic.Services.Shares;
using FileHub.Downloads;
using FileHub.Extensions;
using FileHub.Links;
using Microsoft.Extensions.Options;

namespace FileHub.Endpoints;

/// <summary>
/// The three routes the internet reaches without a cookie. They are deliberately the whole
/// anonymous surface, and deliberately thin: no user lookup, no directory walk, nothing but the
/// share row, one sandbox resolution and a stat.
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

    private static async Task<IResult> GetAsync(Guid id, IShareService service)
    {
        var result = await service.ResolvePublicAsync(id);

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
        IShareService service,
        IOptions<AppOptions> options,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        var result = await service.ResolvePublicAsync(id);

        if (result.HasError)
        {
            // A browser follows this link directly, so a dead or exhausted one is answered with the
            // app's 404 page rather than with a ProblemDetails body it would render as raw JSON.
            return Results.Redirect(ShareLinks.NotFound(options.Value));
        }

        var share = result.Value;

        // Counted before the first byte: a client that disconnects halfway still consumed the link,
        // and counting afterwards would let a limit be evaded by aborting every download.
        await service.RegisterDownloadAsync(id);

        return FileDownload.Create(
            context, share.FullPath, share.Name, share.IsDirectory,
            loggerFactory.CreateLogger(nameof(PublicShareEndpoint)));
    }

    private static async Task<IResult> OpenGraphAsync(Guid id, IShareService service, IOptions<AppOptions> options)
    {
        var result = await service.ResolvePublicAsync(id);

        // A dead link still renders a page rather than a 404: this URL is what gets pasted into a
        // chat, and the crawler that fetches it should get tags, not an error.
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

    /// <summary>Relative on purpose: the redirect stays on whatever host served the page, so a link
    /// opened through an alternative hostname does not bounce the visitor to the configured one.</summary>
    private static string LandingPath(Guid id) => $"/share/{id}";
}
