using Dtos.Auth;
using Entities.Account;
using FileHub.BusinessLogic.Services.Identity;
using FileHub.Extensions;
using Microsoft.AspNetCore.Identity;

namespace FileHub.Endpoints;

/// <summary>
/// The anonymous half of authentication: signing in and out, and the links a user follows while
/// signed out. Login, the two-factor step and logout talk to <c>SignInManager</c> here rather than
/// through a service, because what they produce is the sign-in cookie itself.
/// </summary>
public static class AuthEndpoint
{
    private const string LogCategory = "FileHub.Endpoints.Auth";

    /// <summary>Same reply for an unknown address and a wrong password, so neither can be probed.</summary>
    private const string BadCredentials = "Bad email or password.";

    public static void MapAuthEndpoint(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("api/auth");

        // Both of these are reachable from the internet and both take an email address as input, so
        // they are the two worth rate limiting: one guesses passwords, the other sends mail.
        group.MapPost("login", LoginAsync).RequireRateLimiting("auth");
        group.MapPost("forgot-password", ForgotPasswordAsync).RequireRateLimiting("auth");

        group.MapPost("login-2fa", LoginTwoFactorAsync);
        group.MapPost("logout", LogoutAsync);
        group.MapGet("status", GetAuthStatusAsync);

        group.MapPost("accept-invite", AcceptInviteAsync);
        group.MapPost("reset-password", ResetPasswordAsync);
        group.MapPost("confirm-email-change", ConfirmEmailChangeAsync);
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

    private static async Task<IResult> LoginAsync(
        LoginDto loginDto,
        SignInManager<FileHubUser> signInManager,
        UserManager<FileHubUser> userManager,
        ILoggerFactory loggerFactory
    )
    {
        var logger = loggerFactory.CreateLogger(LogCategory);
        var email = loginDto.Email?.Trim();
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(loginDto.Password))
        {
            return Results.Problem(detail: BadCredentials, statusCode: StatusCodes.Status400BadRequest);
        }

        // Sign-in resolves by email only; a username is a display name and intentionally not an identifier.
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            logger.LogInformation("Failed login attempt for unknown email \"{Email}\"", email);
            return Results.Problem(detail: BadCredentials, statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            loginDto.Password,
            isPersistent: true,
            // This form is on the public internet, so a wrong password has to cost something: enough
            // failures lock the account for the window Identity is configured with.
            lockoutOnFailure: true
        );

        if (result.RequiresTwoFactor)
        {
            // The password was right but no cookie is issued yet: SignInManager parked the user id in
            // the two-factor cookie, and login-2fa finishes the sign-in from there.
            logger.LogInformation("User {UserId} passed the password step and needs a two-factor code", user.Id);
            return Results.Ok(new LoginResultDto { RequiresTwoFactor = true });
        }

        if (result.IsLockedOut)
        {
            logger.LogInformation("Login attempt for locked-out user {UserId}", user.Id);
            return Results.Problem(
                detail: "Too many failed attempts. Please try again later.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (result.IsNotAllowed)
        {
            // Only reachable with the correct password, so naming the reason leaks nothing an attacker
            // doesn't already have — and without it a user who never accepted their invitation is stuck.
            logger.LogInformation("User {UserId} tried to log in with an unconfirmed email address", user.Id);
            return Results.Problem(
                detail: "This account has not been activated yet. Follow the invitation link you were sent.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!result.Succeeded)
        {
            logger.LogInformation("Failed login attempt for user {UserId}", user.Id);
            return Results.Problem(detail: BadCredentials, statusCode: StatusCodes.Status400BadRequest);
        }

        logger.LogInformation("User {UserId} ({Username}) logged in", user.Id, user.UserName);
        return Results.Ok(new LoginResultDto { MustChangePassword = user.MustChangePassword });
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
            Roles = [.. roles],
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
