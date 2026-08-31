using Dal.Repositories.Admin;
using Dal.Repositories.Shares;
using Dtos.Admin;
using Entities.Account;
using FileHub.BusinessLogic.Services.Admin;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// Fixture for the admin user list. The rules worth testing here are the ones that cannot be undone
/// from inside the app: FileHub has no registration page and no self-service way into the admin
/// area, so an install that loses its last usable admin has to be repaired in the database.
/// </summary>
public abstract class AdminTestBase : TestHostBase
{
    protected IUserAdminService Admin => Services.GetRequiredService<IUserAdminService>();
    protected IRoleService Roles => Services.GetRequiredService<IRoleService>();

    protected AdminTestBase() : base(Configure)
    {
    }

    private static void Configure(IServiceCollection services)
    {
        services.AddScoped<IUserAdminRepository, UserAdminRepository>();
        // Taking the right to publish away from an account revokes the links it published, so the
        // admin service reaches the share table.
        services.AddScoped<IShareRepository, ShareRepository>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IRoleService, RoleService>();
    }

    /// <summary>A confirmed, unlocked account holding the Admin role — one that counts as active.</summary>
    protected Task<FileHubUser> CreateAdminAsync(string email) =>
        CreateUserAsync(email, "test-password", Shared.Roles.Admin, Shared.Roles.User);

    protected Task<FileHubUser> CreateMemberAsync(string email) =>
        CreateUserAsync(email, "test-password", Shared.Roles.User);

    /// <summary>The DTO the admin screen posts to leave an account exactly as it is.</summary>
    protected static UpdateUserDto Unchanged(FileHubUser user, params string[] roles) => new()
    {
        Username = user.UserName!,
        Email = user.Email!,
        Roles = roles
    };

    protected async Task<FileHubUser> ReloadAsync(Guid userId)
    {
        Context.ChangeTracker.Clear();
        return await UserManager.FindByIdAsync(userId.ToString())
               ?? throw new InvalidOperationException($"User {userId} is gone.");
    }
}
