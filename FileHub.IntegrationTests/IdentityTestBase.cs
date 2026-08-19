using Dal.Repositories.Admin;
using Dal.Repositories.Identity;
using Dal.Repositories.Shares;
using Dtos.Admin;
using Entities.Account;
using FileHub.BusinessLogic.Services.Admin;
using FileHub.BusinessLogic.Services.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// Fixture for the signed-out flows. It carries <see cref="IUserAdminService"/> as well, because
/// FileHub has no registration page: the only way an account comes into existence is an admin
/// inviting one, so the invitation round trip has to start there.
/// <para>
/// A real <see cref="SignInManager{TUser}"/> is registered so "the account can sign in" can be
/// asserted rather than approximated — it is what enforces <c>RequireConfirmedEmail</c> and the
/// lockout, and an invited account failing either is the whole point of the invitation.
/// </para>
/// </summary>
public abstract class IdentityTestBase : TestHostBase
{
    protected const string Password = "invite-password";

    protected IIdentityService Identity => Services.GetRequiredService<IIdentityService>();
    protected IUserAdminService Admin => Services.GetRequiredService<IUserAdminService>();
    protected SignInManager<FileHubUser> SignIn => Services.GetRequiredService<SignInManager<FileHubUser>>();

    protected IdentityTestBase() : base(Configure)
    {
    }

    private static void Configure(IServiceCollection services)
    {
        // Mirrors Program.cs: an unconfirmed address means the invitation was never accepted, and
        // such an account must not be able to sign in.
        services.Configure<IdentityOptions>(options => options.SignIn.RequireConfirmedEmail = true);

        services.AddHttpContextAccessor();
        // The cookie schemes, so a successful sign-in has somewhere to write the cookie. Without them
        // every failure path still works and only the one that hands out a session throws.
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.AddIdentityCore<FileHubUser>().AddSignInManager();

        services.AddScoped<IIdentityRepository, IdentityRepository>();
        services.AddScoped<IUserAdminRepository, UserAdminRepository>();
        services.AddScoped<IShareRepository, ShareRepository>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
    }

    /// <summary>Creates an account the way the admin screen does and returns the mailed invitation.</summary>
    protected async Task<SentMail> InviteAsync(string username = "ada", string email = "ada@example.com")
    {
        await EnsureRolesAsync();

        var result = await Admin.InviteUserAsync(new InviteUserDto
        {
            Username = username,
            Email = email,
            Roles = [Roles.User]
        });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.True(result.Value.InviteMailSent);

        var mail = Email.Last!;
        Assert.Equal(MailKind.Invite, mail.Kind);
        return mail;
    }

    /// <summary>
    /// Puts a request-shaped <see cref="HttpContext"/> where <c>SignInManager</c> looks for one, so a
    /// successful sign-in has a response to write its cookie onto. Call it from the test method rather
    /// than the constructor: the accessor is <c>AsyncLocal</c>-backed.
    /// </summary>
    protected void UseHttpContext()
    {
        Services.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { RequestServices = Services };
    }

    protected async Task<FileHubUser> ReloadAsync(Guid userId)
    {
        Context.ChangeTracker.Clear();
        return await UserManager.FindByIdAsync(userId.ToString())
               ?? throw new InvalidOperationException($"User {userId} is gone.");
    }
}
