using Dal.Repositories.Admin;
using Dal.Repositories.Identity;
using Dal.Repositories.Shares;
using Dtos.Admin;
using Dtos.Auth;
using Entities.Account;
using FileHub.BusinessLogic.Services.Admin;
using FileHub.BusinessLogic.Services.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// Its own fixture because it needs its own token lifespan: Identity's default is a day, and the
/// only way to reach the expiry branch in a test is to configure a lifespan that has already
/// passed by the time the token is redeemed.
/// </summary>
public sealed class ExpiredTokenTests : TestHostBase
{
    private const string Password = "invite-password";

    private IIdentityService Identity => Services.GetRequiredService<IIdentityService>();
    private IUserAdminService Admin => Services.GetRequiredService<IUserAdminService>();

    public ExpiredTokenTests() : base(Configure)
    {
    }

    private static void Configure(IServiceCollection services)
    {
        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromMilliseconds(1));

        // IdentityRepository signs users in, so it needs a real SignInManager to be resolvable.
        services.AddHttpContextAccessor();
        services.AddAuthentication();
        services.AddIdentityCore<FileHubUser>().AddSignInManager();

        services.AddScoped<IIdentityRepository, IdentityRepository>();
        services.AddScoped<IUserAdminRepository, UserAdminRepository>();
        services.AddScoped<IShareRepository, ShareRepository>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
    }

    [Fact]
    public async Task An_expired_invitation_token_is_rejected()
    {
        await EnsureRolesAsync();
        var invited = await Admin.InviteUserAsync(new InviteUserDto
        {
            Username = "ada",
            Email = "ada@example.com",
            Roles = [Roles.User]
        });
        var mail = Email.Last!;

        await Task.Delay(50);

        var result = await Identity.AcceptInviteAsync(new AcceptInviteDto
        {
            UserId = mail.UserId!.Value.ToString(),
            Token = mail.Token,
            Password = Password,
            DisplayName = string.Empty
        });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);

        // Nothing was half-applied: the account is exactly as the admin left it, so a fresh
        // invitation still works.
        var user = await UserManager.FindByIdAsync(invited.Value.UserId.ToString());
        Assert.False(user!.EmailConfirmed);
        Assert.False(await UserManager.HasPasswordAsync(user));
    }

    [Fact]
    public async Task An_expired_password_reset_token_is_rejected()
    {
        var ada = await CreateUserAsync("ada@example.com");
        await Identity.SendPasswordResetAsync(new ForgotPasswordDto { Email = "ada@example.com" });
        var token = (await Email.WaitForMailAsync()).Token;

        await Task.Delay(50);

        var result = await Identity.ResetPasswordAsync(new ResetPasswordDto
        {
            Email = "ada@example.com",
            Token = token,
            Password = "brand-new-password"
        });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.False(await UserManager.CheckPasswordAsync(ada, "brand-new-password"));
    }
}
