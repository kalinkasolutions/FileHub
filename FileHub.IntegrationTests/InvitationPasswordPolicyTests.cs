using Dal.Repositories.Admin;
using Dal.Repositories.Identity;
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
/// Its own fixture because it needs a password rule the DTO does not mirror. That is the case the
/// ordering inside <c>AcceptInviteAsync</c> exists for: the DTO and <c>IdentityOptions</c> are two
/// separate declarations of the same policy, and the moment they drift the invitation must still
/// either apply completely or not at all.
/// </summary>
public sealed class InvitationPasswordPolicyTests : TestHostBase
{
    private IIdentityService Identity => Services.GetRequiredService<IIdentityService>();
    private IUserAdminService Admin => Services.GetRequiredService<IUserAdminService>();

    public InvitationPasswordPolicyTests() : base(Configure)
    {
    }

    private static void Configure(IServiceCollection services)
    {
        // A rule the DTO says nothing about, standing in for any future drift between the two.
        services.Configure<IdentityOptions>(options => options.Password.RequireUppercase = true);

        services.AddHttpContextAccessor();
        services.AddAuthentication();
        services.AddIdentityCore<FileHubUser>().AddSignInManager();

        services.AddScoped<IIdentityRepository, IdentityRepository>();
        services.AddScoped<IUserAdminRepository, UserAdminRepository>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
    }

    [Fact]
    public async Task A_password_only_Identity_refuses_leaves_the_invitation_untouched()
    {
        await EnsureRolesAsync();
        var invited = await Admin.InviteUserAsync(new InviteUserDto
        {
            Username = "ada",
            Email = "ada@example.com",
            Roles = [Roles.User]
        });
        var mail = Email.Last!;

        var result = await Identity.AcceptInviteAsync(new AcceptInviteDto
        {
            UserId = mail.UserId!.Value.ToString(),
            Token = mail.Token,
            // Passes the DTO, fails IdentityOptions.
            Password = "no-capitals-here",
            DisplayName = string.Empty
        });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);

        // The account is exactly as the admin left it — invited, not activated — rather than confirmed
        // with no password, which is a state the admin screen calls active and resend-invite refuses
        // to repair.
        Context.ChangeTracker.Clear();
        var user = await UserManager.FindByIdAsync(invited.Value.UserId.ToString());
        Assert.False(user!.EmailConfirmed);
        Assert.False(await UserManager.HasPasswordAsync(user));
    }

    [Fact]
    public async Task A_password_only_Identity_refuses_leaves_the_link_usable()
    {
        await EnsureRolesAsync();
        await Admin.InviteUserAsync(new InviteUserDto
        {
            Username = "ada",
            Email = "ada@example.com",
            Roles = [Roles.User]
        });
        var mail = Email.Last!;

        await Identity.AcceptInviteAsync(new AcceptInviteDto
        {
            UserId = mail.UserId!.Value.ToString(),
            Token = mail.Token,
            Password = "no-capitals-here",
            DisplayName = string.Empty
        });

        var result = await Identity.AcceptInviteAsync(new AcceptInviteDto
        {
            UserId = mail.UserId.Value.ToString(),
            Token = mail.Token,
            Password = "Capitals-Here",
            DisplayName = string.Empty
        });

        Assert.True(result.IsSuccess, result.ErrorMessage);
    }
}
