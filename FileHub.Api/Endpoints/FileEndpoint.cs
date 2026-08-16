using System.Security.Claims;
using FileHub.BusinessLogic.Services.Files;
using FileHub.Downloads;
using FileHub.Extensions;
using Dtos.Files;
using Shared;

namespace FileHub.Endpoints;

/// <summary>
/// Browsing and downloading. Everything here is authenticated and scoped to the base paths the
/// caller can reach: their own grants, the grants of their groups, and — for an admin — all of
/// them. The role is read off the principal here and passed down as an argument, so no layer below
/// has to guess where the access came from.
/// </summary>
public static class FileEndpoint
{
    public static void MapFileEndpoint(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("api/files").RequireAuthorization();

        group.MapGet("", GetBasePathsAsync);
        group.MapPost("navigate", NavigateAsync);
        group.MapGet("download/{basePathId:guid}/{*relativePath}", DownloadAsync);
    }

    private static async Task<IResult> GetBasePathsAsync(ClaimsPrincipal user, IFileService service)
    {
        return (await service.GetBasePathsAsync(user.GetUserId(), user.IsInRole(Roles.Admin))).ToHttpResult();
    }

    private static async Task<IResult> NavigateAsync(NavigateDto dto, ClaimsPrincipal user, IFileService service)
    {
        return (await service.NavigateAsync(user.GetUserId(), user.IsInRole(Roles.Admin), dto)).ToHttpResult();
    }

    private static async Task<IResult> DownloadAsync(
        Guid basePathId,
        string? relativePath,
        ClaimsPrincipal user,
        IFileService service,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        var result = await service.ResolveDownloadAsync(
            user.GetUserId(), user.IsInRole(Roles.Admin), basePathId, relativePath ?? string.Empty);

        if (result.HasError)
        {
            return result.ToHttpResult();
        }

        var resolved = result.Value;
        return FileDownload.Create(
            context, resolved.FullPath, resolved.Name, resolved.IsDirectory,
            loggerFactory.CreateLogger(nameof(FileEndpoint)));
    }
}
