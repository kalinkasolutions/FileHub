using System.Security.Claims;
using FileHub.BusinessLogic.Services.Media;
using FileHub.Extensions;
using Shared;

namespace FileHub.Endpoints;

/// <summary>
/// The info button: what TMDb or IGDB knows about a folder, and its poster. A session is enough —
/// the service answers on the base path's own access rules, exactly like a listing — and only an
/// admin may force a fresh look-up, because each one is a request to a third party.
/// </summary>
public static class MediaInfoEndpoint
{
    public static void MapMediaInfoEndpoint(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("api/media").RequireAuthorization();

        group.MapGet("info", GetAsync);
        group.MapPost("info/refresh", RefreshAsync)
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));
        group.MapGet("poster/{id:guid}", GetPosterAsync);
    }

    private static async Task<IResult> GetAsync(
        Guid basePathId, string? path, ClaimsPrincipal user, IMediaInfoService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(
            user.GetUserId(), user.IsInRole(Roles.Admin), basePathId, path ?? string.Empty,
            refresh: false, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> RefreshAsync(
        Guid basePathId, string? path, ClaimsPrincipal user, IMediaInfoService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(
            user.GetUserId(), user.IsInRole(Roles.Admin), basePathId, path ?? string.Empty,
            refresh: true, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetPosterAsync(
        Guid id, ClaimsPrincipal user, IMediaInfoService service, HttpContext context)
    {
        var result = await service.GetPosterPathAsync(user.GetUserId(), user.IsInRole(Roles.Admin), id);

        if (!result.IsSuccess)
        {
            return result.ToHttpResult();
        }

        // Private: the poster is answered on the base path's access rules, so a shared cache must
        // not hand it to the next caller. A day, because a re-fetch keeps the same id.
        context.Response.Headers.CacheControl = "private, max-age=86400";
        return Results.File(result.Value, "image/jpeg");
    }
}
