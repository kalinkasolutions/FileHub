using System.Security.Claims;
using Dtos.Groups;
using FileHub.BusinessLogic.Services.Groups;
using FileHub.Extensions;
using Shared;

namespace FileHub.Endpoints;

/// <summary>
/// Groups. Two route groups, because they are two different audiences:
/// <list type="bullet">
/// <item><c>api/admin/groups</c> — admin-only. A group is part of the access model, so creating one,
/// renaming it, and deciding who is in it and what it grants all live behind the admin role.</item>
/// <item><c>api/groups</c> — any signed-in caller, and read-only: the groups they may aim a share
/// at. Without it, picking an audience would mean handing every user the admin group list.</item>
/// </list>
/// The base-path side of the grant (<c>api/admin/base-path/{id}/groups</c>) lives in
/// <see cref="BasePathEndpoint"/>, next to the user-keyed pair it mirrors.
/// </summary>
public static class GroupEndpoint
{
    public static void MapGroupEndpoint(this IEndpointRouteBuilder builder)
    {
        var admin = builder.MapGroup("api/admin/groups")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        admin.MapGet("", ListAsync);
        admin.MapPost("", CreateAsync);
        admin.MapPut("{id:guid}", RenameAsync);
        admin.MapDelete("{id:guid}", DeleteAsync);

        admin.MapGet("{id:guid}/members", GetMembersAsync);
        admin.MapPut("{id:guid}/members", SetMembersAsync);

        admin.MapGet("{id:guid}/base-paths", GetBasePathsAsync);
        admin.MapPut("{id:guid}/base-paths", SetBasePathsAsync);

        var caller = builder.MapGroup("api/groups").RequireAuthorization();

        caller.MapGet("", ListForCallerAsync);
    }

    private static async Task<IResult> ListAsync(IGroupService service)
    {
        return (await service.ListAsync()).ToHttpResult();
    }

    private static async Task<IResult> CreateAsync(SaveGroupDto dto, IGroupService service)
    {
        return (await service.CreateAsync(dto)).ToHttpResult();
    }

    private static async Task<IResult> RenameAsync(Guid id, SaveGroupDto dto, IGroupService service)
    {
        return (await service.RenameAsync(id, dto)).ToHttpResult();
    }

    private static async Task<IResult> DeleteAsync(Guid id, IGroupService service)
    {
        return (await service.DeleteAsync(id)).ToHttpResult();
    }

    private static async Task<IResult> GetMembersAsync(Guid id, IGroupService service)
    {
        return (await service.GetMembersAsync(id)).ToHttpResult();
    }

    private static async Task<IResult> SetMembersAsync(Guid id, SetGroupMembersDto dto, IGroupService service)
    {
        return (await service.SetMembersAsync(id, dto)).ToHttpResult();
    }

    private static async Task<IResult> GetBasePathsAsync(Guid id, IGroupService service)
    {
        return (await service.GetBasePathsAsync(id)).ToHttpResult();
    }

    private static async Task<IResult> SetBasePathsAsync(Guid id, SetGroupBasePathsDto dto, IGroupService service)
    {
        return (await service.SetBasePathsAsync(id, dto)).ToHttpResult();
    }

    private static async Task<IResult> ListForCallerAsync(ClaimsPrincipal user, IGroupService service)
    {
        return (await service.ListForCallerAsync(user.GetUserId(), user.IsInRole(Roles.Admin))).ToHttpResult();
    }
}
