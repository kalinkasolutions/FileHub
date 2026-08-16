using Dtos.BasePaths;
using FileHub.BusinessLogic.Services.BasePaths;
using FileHub.Extensions;
using Shared;

namespace FileHub.Endpoints;

/// <summary>
/// Admin-only administration of the base paths and of the grants that make one visible to a user.
/// <para>
/// The grant table is edited from both ends, so this file maps two groups: the base-path-keyed
/// routes under <c>api/admin/base-path</c> and the two user-keyed ones under <c>api/admin/users</c>.
/// The rest of <c>api/admin/users</c> belongs to the account administration endpoint.
/// </para>
/// </summary>
public static class BasePathEndpoint
{
    public static void MapBasePathEndpoint(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("api/admin/base-path")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        group.MapGet("", GetAllAsync);
        group.MapPost("", CreateAsync);
        group.MapPut("{id:guid}", UpdateAsync);
        group.MapDelete("{id:guid}", DeleteAsync);

        group.MapGet("{id:guid}/users", GetUsersAsync);
        group.MapPut("{id:guid}/users", SetUsersAsync);

        // The group half of the same grant, edited from the base-path end. The group end of it is
        // api/admin/groups/{id}/base-paths, in GroupEndpoint.
        group.MapGet("{id:guid}/groups", GetGroupsAsync);
        group.MapPut("{id:guid}/groups", SetGroupsAsync);

        var users = builder.MapGroup("api/admin/users")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        users.MapGet("{userId:guid}/base-paths", GetUserBasePathsAsync);
        users.MapPut("{userId:guid}/base-paths", SetUserBasePathsAsync);
    }

    private static async Task<IResult> GetAllAsync(IBasePathService service)
    {
        return (await service.GetAllAsync()).ToHttpResult();
    }

    private static async Task<IResult> CreateAsync(SaveBasePathDto dto, IBasePathService service)
    {
        return (await service.CreateAsync(dto)).ToHttpResult();
    }

    private static async Task<IResult> UpdateAsync(Guid id, SaveBasePathDto dto, IBasePathService service)
    {
        return (await service.UpdateAsync(id, dto)).ToHttpResult();
    }

    private static async Task<IResult> DeleteAsync(Guid id, IBasePathService service)
    {
        return (await service.DeleteAsync(id)).ToHttpResult();
    }

    private static async Task<IResult> GetUsersAsync(Guid id, IBasePathService service)
    {
        return (await service.GetUsersAsync(id)).ToHttpResult();
    }

    private static async Task<IResult> SetUsersAsync(Guid id, SetBasePathAccessDto dto, IBasePathService service)
    {
        return (await service.SetUsersAsync(id, dto)).ToHttpResult();
    }

    private static async Task<IResult> GetGroupsAsync(Guid id, IBasePathService service)
    {
        return (await service.GetGroupsAsync(id)).ToHttpResult();
    }

    private static async Task<IResult> SetGroupsAsync(Guid id, SetBasePathGroupsDto dto, IBasePathService service)
    {
        return (await service.SetGroupsAsync(id, dto)).ToHttpResult();
    }

    private static async Task<IResult> GetUserBasePathsAsync(Guid userId, IBasePathService service)
    {
        return (await service.GetUserBasePathsAsync(userId)).ToHttpResult();
    }

    private static async Task<IResult> SetUserBasePathsAsync(
        Guid userId, SetUserBasePathsDto dto, IBasePathService service)
    {
        return (await service.SetUserBasePathsAsync(userId, dto)).ToHttpResult();
    }
}
