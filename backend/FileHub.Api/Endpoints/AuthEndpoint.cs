using Dtos.Auth;
using Entities.Account;
using FileHub.BusinessLogic.Services.Identity;
using FileHub.Extensions;
using Microsoft.AspNetCore.Identity;
using Shared;

namespace FileHub.Endpoints;

/// <summary>
/// The anonymous half of authentication: signing in and out, and the links a user follows while
/// signed out. The two-factor step and logout talk to <c>SignInManager</c> here rather than through a
/// service, because what they produce is the sign-in cookie itself; the password step went the other
/// way, into <see cref="IIdentityService"/>, so that its DTO is validated and its rules — one answer
/// for every failure a stranger can provoke — are testable without HTTP.
/// </summary>
public static class AuthEndpoint
{
    private const string LogCategory = "FileHub.Endpoints.Auth";

    public static void MapAuthEndpoint(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("api/auth");

        // Every anonymous route here that checks a credential is limited, because all of them are
        // reachable from the internet and none of them costs the caller anything to retry.
        //
        // login guesses passwords and forgot-password sends mail; login-2fa is the second half of
        // the same sign-in and guesses a six-digit code, so limiting the first half and not the
        // second only moves where an attacker spends their attempts. The three link routes redeem a
        // token: those tokens are unguessable, so the limit is not what stops them being forged —
        // it is what stops the routes being a free, unmetered way to make the server do work.
        //
        // One "auth" policy, so they share a per-address budget rather than each getting their own.
        group.MapPost("login", LoginAsync).RequireRateLimiting("auth");
        group.MapPost("forgot-password", ForgotPasswordAsync).RequireRateLimiting("auth");
        group.MapPost("login-2fa", LoginTwoFactorAsync).RequireRateLimiting("auth");

        // Not limited: neither checks a credential. Logout ends a session the caller already has,
        // and status is what the SPA asks on every page load.
        group.MapPost("logout", LogoutAsync);
        group.MapGet("status", GetAuthStatusAsync);

        group.MapPost("accept-invite", AcceptInviteAsync).RequireRateLimiting("auth");
        group.MapPost("reset-password", ResetPasswordAsync).RequireRateLimiting("auth");
        group.MapPost("confirm-email-change", ConfirmEmailChangeAsync).RequireRateLimiting("auth");
    }

    private static async Task<IResult> AcceptInviteAsync(AcceptInviteDto acceptInviteDto, IIdentityService identityService)
    {
        return (await identityService.AcceptInviteAsync(acceptInviteDto)).ToHttpResult();
    }

    private static async Task<IResult> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto, IIdentityService identityService)
    {
        return (await identityService.SendPasswordResetAsync(forgotPasswordDto)).ToHttpResult();
    }

    private static async Task<IResult> ResetPasswordAsync(ResetPasswordDto resetPasswordDto, IIdentityService identityService)
    {
        return (await identityService.ResetPasswordAsync(resetPasswordDto)).ToHttpResult();
    }

    private static async Task<IResult> ConfirmEmailChangeAsync(
        ConfirmEmailChangeDto confirmEmailChangeDto, IIdentityService identityService)
    {
        return (await identityService.ConfirmEmailChangeAsync(confirmEmailChangeDto)).ToHttpResult();
    }

    private static async Task<IResult> LoginAsync(LoginDto loginDto, IIdentityService identityService)
    {
        return (await identityService.LoginAsync(loginDto)).ToHttpResult();
    }

    private static async Task<IResult> LoginTwoFactorAsync(
        TwoFactorLoginDto twoFactorLoginDto,
        SignInManager<FileHubUser> signInManager,
        ILoggerFactory loggerFactory
    )
    {
        var logger = loggerFactory.CreateLogger(LogCategory);
        var code = twoFactorLoginDto.Code?.Trim();
        if (string.IsNullOrEmpty(code))
        {
            return Results.Problem(
                detail: "Enter the code from your authenticator app.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
        {
            return Results.Problem(
                detail: "This sign-in has expired. Please enter your email and password again.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Six digits is an authenticator code; anything else is treated as a recovery code, which
        // Identity stores (and therefore matches) with its hyphen intact.
        var authenticatorCode = code
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        var isAuthenticatorCode = authenticatorCode.Length == 6 && authenticatorCode.All(char.IsAsciiDigit);

        var result = isAuthenticatorCode
            ? await signInManager.TwoFactorAuthenticatorSignInAsync(
                authenticatorCode, isPersistent: true, rememberClient: twoFactorLoginDto.RememberMachine)
            : await signInManager.TwoFactorRecoveryCodeSignInAsync(
                code.Replace(" ", string.Empty, StringComparison.Ordinal));

        if (!result.Succeeded)
        {
            logger.LogInformation(
                "Failed two-factor attempt for user {UserId} ({CodeKind})",
                user.Id, isAuthenticatorCode ? "authenticator code" : "recovery code");
            return Results.Problem(
                detail: isAuthenticatorCode
                    ? "That code isn't valid. Check your device's clock and try the next code."
                    : "That recovery code isn't valid or has already been used.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        logger.LogInformation(
            "User {UserId} completed two-factor sign-in with a {CodeKind}",
            user.Id, isAuthenticatorCode ? "authenticator code" : "recovery code");
        return Results.Ok(new LoginResultDto { MustChangePassword = user.MustChangePassword });
    }

    private static async Task<IResult> GetAuthStatusAsync(HttpContext httpContext, UserManager<FileHubUser> userManager)
    {
        // Answers an anonymous caller with the same shape, all of it empty, because the SPA asks this
        // before it knows whether it has a session at all.
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return Results.Ok(new AuthStatusDto());
        }

        var user = await userManager.GetUserAsync(httpContext.User);
        if (user is null)
        {
            // A cookie for an account that has since been deleted; treat it as no session.
            return Results.Ok(new AuthStatusDto());
        }

        var roles = await userManager.GetRolesAsync(user);

        return Results.Ok(new AuthStatusDto
        {
            Authenticated = true,
            UserId = user.Id,
            Username = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            // Effective, not stored: the admin role implies the others, and the client uses this to
            // decide what to offer. Answering with the stored set would hide from an admin a button
            // the API would have honoured.
            Roles = [.. Roles.Effective(roles)],
            MustChangePassword = user.MustChangePassword
        });
    }

    private static async Task<IResult> LogoutAsync(
        SignInManager<FileHubUser> signInManager, HttpContext httpContext, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(LogCategory);
        // Logout isn't behind RequireAuthorization, so the principal may carry no id — sign out regardless.
        httpContext.User.TryGetUserId(out var userId);
        await signInManager.SignOutAsync();
        logger.LogInformation("User {UserId} logged out", userId);
        return Results.Ok();
    }
}
