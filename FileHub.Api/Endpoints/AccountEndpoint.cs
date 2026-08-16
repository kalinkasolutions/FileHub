using System.Security.Claims;
using Dtos.Account;
using Entities.Account;
using FileHub.BusinessLogic.Services.Account;
using FileHub.Extensions;
using Microsoft.AspNetCore.Identity;

namespace FileHub.Endpoints;

/// <summary>
/// Account self-service for the signed-in user. Thin like every other endpoint: bind, call the
/// service, <c>ToHttpResult()</c> — with one extra concern of its own. Most of these operations rotate
/// the user's security stamp (Identity does that on a password, username, authenticator-secret or
/// two-factor change), which invalidates every cookie issued to them, this one included. Refreshing
/// the caller's sign-in afterwards is what keeps "sign my other devices out" from signing out the
/// device asking for it — and is also what rewrites the must-change-password claim.
/// </summary>
public static class AccountEndpoint
{
    public static void MapAccountEndpoint(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("api/account").RequireAuthorization();

        group.MapGet("", GetAsync);
        group.MapPut("username", UpdateUsernameAsync);
        group.MapPost("password", ChangePasswordAsync);
        group.MapPost("email", RequestEmailChangeAsync);
        group.MapPost("sign-out-everywhere", SignOutEverywhereAsync);

        group.MapGet("2fa/setup", GetTwoFactorSetupAsync);
        group.MapPost("2fa/enable", EnableTwoFactorAsync);
        group.MapPost("2fa/disable", DisableTwoFactorAsync);
        group.MapPost("2fa/recovery-codes", RegenerateRecoveryCodesAsync);
    }

    private static async Task<IResult> GetAsync(ClaimsPrincipal user, IAccountService accountService)
    {
        return (await accountService.GetAsync(user.GetUserId())).ToHttpResult();
    }

    private static async Task<IResult> UpdateUsernameAsync(
        UpdateUsernameDto dto,
        ClaimsPrincipal user,
        IAccountService accountService,
        SignInManager<FileHubUser> signInManager
    )
    {
        var result = await accountService.UpdateUsernameAsync(user.GetUserId(), dto);
        return await result.ToRefreshedHttpResultAsync(signInManager, user);
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordDto dto,
        ClaimsPrincipal user,
        IAccountService accountService,
        SignInManager<FileHubUser> signInManager
    )
    {
        // Other devices lose their cookie (that's the point); this one keeps working, and its new
        // cookie is written without the must-change-password claim the old one carried.
        var result = await accountService.ChangePasswordAsync(user.GetUserId(), dto);
        return await result.ToRefreshedHttpResultAsync(signInManager, user);
    }

    private static async Task<IResult> RequestEmailChangeAsync(
        ChangeEmailDto dto, ClaimsPrincipal user, IAccountService accountService)
    {
        // Nothing changes on the account yet — only a mail goes out — so there is no stamp to outrun.
        return (await accountService.RequestEmailChangeAsync(user.GetUserId(), dto)).ToHttpResult();
    }

    private static async Task<IResult> SignOutEverywhereAsync(
        ClaimsPrincipal user,
        IAccountService accountService,
        SignInManager<FileHubUser> signInManager
    )
    {
        var result = await accountService.SignOutEverywhereAsync(user.GetUserId());
        return await result.ToRefreshedHttpResultAsync(signInManager, user);
    }

    private static async Task<IResult> GetTwoFactorSetupAsync(
        ClaimsPrincipal user, IAccountService accountService, SignInManager<FileHubUser> signInManager)
    {
        // Generating the authenticator secret rotates the stamp, so even opening the setup screen has
        // to refresh the cookie — otherwise the user is signed out part-way through setting 2FA up.
        var result = await accountService.GetTwoFactorSetupAsync(user.GetUserId());
        return await result.ToRefreshedHttpResultAsync(signInManager, user);
    }

    private static async Task<IResult> EnableTwoFactorAsync(
        EnableTwoFactorDto dto,
        ClaimsPrincipal user,
        IAccountService accountService,
        SignInManager<FileHubUser> signInManager
    )
    {
        var result = await accountService.EnableTwoFactorAsync(user.GetUserId(), dto);
        return await result.ToRefreshedHttpResultAsync(signInManager, user);
    }

    private static async Task<IResult> DisableTwoFactorAsync(
        DisableTwoFactorDto dto,
        ClaimsPrincipal user,
        IAccountService accountService,
        SignInManager<FileHubUser> signInManager
    )
    {
        var result = await accountService.DisableTwoFactorAsync(user.GetUserId(), dto);
        return await result.ToRefreshedHttpResultAsync(signInManager, user);
    }

    private static async Task<IResult> RegenerateRecoveryCodesAsync(
        ClaimsPrincipal user, IAccountService accountService, SignInManager<FileHubUser> signInManager)
    {
        var result = await accountService.RegenerateRecoveryCodesAsync(user.GetUserId());
        return await result.ToRefreshedHttpResultAsync(signInManager, user);
    }
}
