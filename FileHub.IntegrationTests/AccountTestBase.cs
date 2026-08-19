using Dal.Repositories.Account;
using Dal.Repositories.Identity;
using Dtos.Account;
using Entities.Account;
using FileHub.BusinessLogic.Services.Account;
using FileHub.BusinessLogic.Services.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// Fixture for the signed-in user's own account. <see cref="IIdentityService"/> comes along because
/// the email change is a two-phase flow whose second half is anonymous — the confirmation link is
/// usually opened in the mail app's browser, where nobody is signed in.
/// </summary>
public abstract class AccountTestBase : TestHostBase
{
    protected const string Password = "account-password";

    protected IAccountService Account => Services.GetRequiredService<IAccountService>();
    protected IIdentityService Identity => Services.GetRequiredService<IIdentityService>();

    protected AccountTestBase() : base(Configure)
    {
    }

    private static void Configure(IServiceCollection services)
    {
        // IdentityRepository signs users in, so it needs a real SignInManager — which in turn needs
        // somewhere to look for an HttpContext. Nothing here reaches the cookie-writing path.
        services.AddHttpContextAccessor();
        services.AddAuthentication();
        services.AddIdentityCore<FileHubUser>().AddSignInManager();

        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IIdentityRepository, IdentityRepository>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IIdentityService, IdentityService>();
    }

    protected Task<FileHubUser> CreateAccountAsync(string email = "ada@example.com") =>
        CreateUserAsync(email, Password);

    protected async Task<FileHubUser> ReloadAsync(Guid userId)
    {
        Context.ChangeTracker.Clear();
        return await UserManager.FindByIdAsync(userId.ToString())
               ?? throw new InvalidOperationException($"User {userId} is gone.");
    }

    /// <summary>Starts the setup with the account password, the way the screen does.</summary>
    protected Task<OperationResult<TwoFactorSetupDto>> StartTwoFactorSetupAsync(
        Guid userId, string currentPassword = Password) =>
        Account.StartTwoFactorSetupAsync(userId, new StartTwoFactorSetupDto { CurrentPassword = currentPassword });

    /// <summary>Runs the real setup + verify flow and returns the recovery codes it issued.</summary>
    protected async Task<List<string>> EnableTwoFactorAsync(Guid userId)
    {
        var setup = await StartTwoFactorSetupAsync(userId);
        var result = await Account.EnableTwoFactorAsync(userId, new EnableTwoFactorDto
        {
            Code = TotpCode.Current(setup.Value.SharedKey),
            CurrentPassword = Password
        });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        return result.Value.Codes;
    }
}
