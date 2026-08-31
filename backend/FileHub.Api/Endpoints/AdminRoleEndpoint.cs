using FileHub.BusinessLogic.Services.Admin;
using FileHub.Extensions;
using Shared;

namespace FileHub.Endpoints;

/// <summary>
/// The role list the user form picks from. Read-only: roles are seeded constants, so there is no
/// create or delete route to map.
/// </summary>
public static class AdminRoleEndpoint
{
    public static void MapAdminRoleEndpoint(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("api/admin/roles")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        group.MapGet("", ListAsync);
    }

    private static async Task<IResult> ListAsync(IRoleService roleService)
    {
        return (await roleService.ListRolesAsync()).ToHttpResult();
    }
}
