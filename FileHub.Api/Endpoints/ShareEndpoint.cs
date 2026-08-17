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

        // Publishing needs the CreateShares role on top of a session; listing and revoking do not.
        // A user whose role was taken away has no links left — losing it revokes them — but one who
        // never had it can still be walked through the screen without being lied to, and an account
        // that keeps a link must always be able to take it down.
        group.MapPost("", CreateAsync)
            .RequireAuthorization(policy => policy.RequireRole(Roles.CreateShares));

        group.MapGet("", ListAsync);
        group.MapDelete("{id:guid}", DeleteAsync);

        var admin = builder.MapGroup("api/admin").RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        admin.MapGet("shares", ListAllAsync);
        admin.MapDelete("share/{id:guid}", AdminDeleteAsync);
    }

    private static async Task<IResult> CreateAsync(
        CreateShareDto dto, ClaimsPrincipal user, IShareService service, IOptions<AppOptions> options)
    {
        // The role travels as an argument, like callerIsAdmin, so the rule is visible in the service
        // and a service-level test can set either answer. The route policy above is the control; this
        // is what makes the service refuse on its own rather than trusting that a route was mapped
        // correctly. An admin holds the role implicitly, through the claims factory.
        var result = await service.CreateAsync(
            user.GetUserId(), user.IsInRole(Roles.Admin), user.IsInRole(Roles.CreateShares), dto);

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
