using System.Security.Claims;
using Dtos.Admin;
using FileHub.BusinessLogic.Services.Admin;
using FileHub.Extensions;
using Shared;

namespace FileHub.Endpoints;

/// <summary>
/// The admin's user management. Every route in the group is admin-only: this is the surface that
/// creates accounts, and FileHub has no registration page, so nothing else may reach it.
/// The self-protection rules (no deleting or disabling yourself, no removing the last admin) live
/// in the service — the endpoint only hands it the caller's id.
/// </summary>
public static class AdminUserEndpoint
{
    public static void MapAdminUserEndpoint(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("api/admin/users")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        group.MapGet("", ListAsync);
        group.MapPost("", InviteAsync);
        group.MapPost("{id:guid}/resend-invite", ResendInviteAsync);
        group.MapPut("{id:guid}", UpdateAsync);
        group.MapPut("{id:guid}/lockout", SetLockoutAsync);
        group.MapDelete("{id:guid}", DeleteAsync);
    }

    private static async Task<IResult> ListAsync(IUserAdminService userAdminService)
    {
        return (await userAdminService.ListUsersAsync()).ToHttpResult();
    }

    private static async Task<IResult> InviteAsync(InviteUserDto dto, IUserAdminService userAdminService)
    {
        return (await userAdminService.InviteUserAsync(dto)).ToHttpResult();
    }

    private static async Task<IResult> ResendInviteAsync(Guid id, IUserAdminService userAdminService)
    {
        return (await userAdminService.ResendInviteAsync(id)).ToHttpResult();
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateUserDto dto, IUserAdminService userAdminService)
    {
        return (await userAdminService.UpdateUserAsync(id, dto)).ToHttpResult();
    }

    private static async Task<IResult> SetLockoutAsync(
        Guid id, SetLockoutDto dto, ClaimsPrincipal user, IUserAdminService userAdminService)
    {
        return (await userAdminService.SetLockoutAsync(user.GetUserId(), id, dto)).ToHttpResult();
    }

    private static async Task<IResult> DeleteAsync(Guid id, ClaimsPrincipal user, IUserAdminService userAdminService)
    {
        return (await userAdminService.DeleteUserAsync(user.GetUserId(), id)).ToHttpResult();
    }
}
