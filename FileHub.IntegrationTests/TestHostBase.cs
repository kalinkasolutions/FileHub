using Dal;
using Entities.Account;
using FileHub.BusinessLogic.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// Base fixture for the service-level integration tests: the real service → repository → EF /
/// Identity stack over a fresh SQLite in-memory database, one per test. SQLite rather than the
/// EF in-memory provider because the unique indexes and the ON DELETE CASCADE behaviour that
/// deleting a user or a base path relies on only exist in a real database.
/// </summary>
public abstract class TestHostBase : IDisposable
{
    private readonly SqliteConnection m_connection;
    private readonly ServiceProvider m_provider;
    private readonly IServiceScope m_scope;

    protected IServiceProvider Services => m_scope.ServiceProvider;
    protected FileHubContext Context { get; }
    protected UserManager<FileHubUser> UserManager { get; }
    protected RoleManager<IdentityRole<Guid>> RoleManager { get; }
    protected FakeEmailService Email { get; }

    /// <param name="configureServices">
    /// Registers the services under test. A delegate rather than an overridable method, because
    /// the fixture has to run it before it can build the provider — and calling a virtual member
    /// from a constructor would run it against a half-constructed derived fixture.
    /// </param>
    protected TestHostBase(Action<IServiceCollection> configureServices)
    {
        ArgumentNullException.ThrowIfNull(configureServices);

        // The connection has to stay open: an in-memory SQLite database lives exactly as long as
        // the connection that created it.
        m_connection = new SqliteConnection("DataSource=:memory:");
        m_connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        // Identity's email-confirmation and password-reset tokens come from the
        // DataProtectorTokenProvider, and the SMTP password is protected the same way.
        services.AddDataProtection();
        services.AddDbContext<FileHubContext>(options => options.UseSqlite(m_connection));

        services.AddIdentityCore<FileHubUser>(options =>
            {
                // Mirrors Program.cs, except for the password length: tests use short passwords.
                options.Password.RequiredLength = 4;
                options.Password.RequireDigit = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = string.Empty;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<FileHubContext>()
            .AddDefaultTokenProviders();

        Email = new FakeEmailService();
        services.AddSingleton<IEmailService>(Email);

        configureServices(services);

        m_provider = services.BuildServiceProvider();
        m_scope = m_provider.CreateScope();

        Context = m_scope.ServiceProvider.GetRequiredService<FileHubContext>();
        Context.Database.EnsureCreated();

        UserManager = m_scope.ServiceProvider.GetRequiredService<UserManager<FileHubUser>>();
        RoleManager = m_scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    }

    /// <summary>Creates both roles, so a test can assign them the way <c>Seed</c> would have.</summary>
    protected async Task EnsureRolesAsync()
    {
        foreach (var role in Roles.All)
        {
            if (!await RoleManager.RoleExistsAsync(role))
            {
                await RoleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }
    }

    protected async Task<FileHubUser> CreateUserAsync(string email, string password = "test-password", params string[] roles)
    {
        var user = new FileHubUser
        {
            UserName = email.Split('@')[0],
            Email = email,
            EmailConfirmed = true
        };

        var created = await UserManager.CreateAsync(user, password);
        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Description)));

        if (roles.Length > 0)
        {
            await EnsureRolesAsync();
            await UserManager.AddToRolesAsync(user, roles);
        }

        return user;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        m_scope.Dispose();
        m_provider.Dispose();
        m_connection.Dispose();
    }
}
