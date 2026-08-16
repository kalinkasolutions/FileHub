using System.Security.Claims;
using Dtos.Shares;
using FileHub.BusinessLogic;
using FileHub.BusinessLogic.Services.Shares;
using FileHub.Extensions;
using FileHub.Links;
using Microsoft.Extensions.Options;
using Shared;

namespace FileHub.Endpoints;

/// <summary>
/// Creating and managing public links. The links themselves are redeemed anonymously through
/// <see cref="PublicShareEndpoint"/>; everything here needs a cookie, and the admin half needs the
/// admin role on top.
/// </summary>
public static class ShareEndpoint
{
    public static void MapShareEndpoint(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("api/share").RequireAuthorization();

        group.MapPost("", CreateAsync);
        group.MapGet("", ListAsync);
        group.MapDelete("{id:guid}", DeleteAsync);

        var admin = builder.MapGroup("api/admin").RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        admin.MapGet("shares", ListAllAsync);
        admin.MapDelete("share/{id:guid}", AdminDeleteAsync);
    }

    private static async Task<IResult> CreateAsync(
        CreateShareDto dto, ClaimsPrincipal user, IShareService service, IOptions<AppOptions> options)
    {
        var result = await service.CreateAsync(user.GetUserId(), user.IsInRole(Roles.Admin), dto);

        if (result.IsSuccess)
        {
            result.Value.Link = ShareLinks.Share(options.Value, result.Value.Id);
        }

        return result.ToHttpResult();
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal user, IShareService service, IOptions<AppOptions> options)
    {
        var result = await service.ListForUserAsync(user.GetUserId());

        if (result.IsSuccess)
        {
            // The public address is the API's to know, not the service's, so the link is stamped on
            // the way out rather than built three layers down.
            foreach (var share in result.Value)
            {
                share.Link = ShareLinks.Share(options.Value, share.Id);
            }
        }

        return result.ToHttpResult();
    }

    private static async Task<IResult> DeleteAsync(Guid id, ClaimsPrincipal user, IShareService service)
    {
        return (await service.DeleteAsync(user.GetUserId(), user.IsInRole(Roles.Admin), id)).ToHttpResult();
    }

    private static async Task<IResult> ListAllAsync(IShareService service, IOptions<AppOptions> options)
    {
        var result = await service.ListAllAsync();

        if (result.IsSuccess)
        {
            foreach (var share in result.Value)
            {
                share.Link = ShareLinks.Share(options.Value, share.Id);
            }
        }

        return result.ToHttpResult();
    }

    private static async Task<IResult> AdminDeleteAsync(Guid id, ClaimsPrincipal user, IShareService service)
    {
        return (await service.DeleteAsync(user.GetUserId(), callerIsAdmin: true, id)).ToHttpResult();
    }
}
