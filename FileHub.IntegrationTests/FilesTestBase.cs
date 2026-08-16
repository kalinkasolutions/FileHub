using Dal.Repositories.BasePaths;
using Dal.Repositories.Shares;
using Dtos.BasePaths;
using FileHub.BusinessLogic.Services.BasePaths;
using FileHub.BusinessLogic.Services.Files;
using FileHub.BusinessLogic.Services.Shares;
using Microsoft.Extensions.DependencyInjection;

namespace FileHub.IntegrationTests;

/// <summary>
/// Fixture for the browsing slice: <see cref="FileService"/> over the real base-path repository and
/// a real directory tree on disk. <see cref="BasePathService"/> comes along because a grant is what
/// makes anything visible at all, and <see cref="ShareService"/> because "cannot see it" has to mean
/// "cannot share it either".
/// </summary>
public abstract class FilesTestBase : TestHostBase
{
    protected TempTree Tree { get; } = new();
    protected IFileService Files => Services.GetRequiredService<IFileService>();
    protected IBasePathService BasePaths => Services.GetRequiredService<IBasePathService>();
    protected IShareService Shares => Services.GetRequiredService<IShareService>();

    protected FilesTestBase() : base(Configure)
    {
    }

    private static void Configure(IServiceCollection services)
    {
        services.AddScoped<IBasePathRepository, BasePathRepository>();
        services.AddScoped<IShareRepository, ShareRepository>();
        services.AddScoped<IBasePathService, BasePathService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IShareService, ShareService>();
    }

    /// <summary>Registers a directory as a base path, through the same service an admin would use.</summary>
    protected async Task<BasePathDto> CreateBasePathAsync(string path, string name = "Media")
    {
        var result = await BasePaths.CreateAsync(new SaveBasePathDto { Path = path, Name = name });
        Assert.True(result.IsSuccess, result.ErrorMessage);
        return result.Value;
    }

    /// <summary>Replaces the set of users granted a base path — an id left out is a revocation.</summary>
    protected async Task GrantAsync(Guid basePathId, params Guid[] userIds)
    {
        var result = await BasePaths.SetUsersAsync(basePathId, new SetBasePathAccessDto { UserIds = [.. userIds] });
        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    /// <summary>
    /// Stands in for the request boundary. Every HTTP request gets its own scoped
    /// <c>FileHubContext</c>; inside one test they share one, and EF hands back the entity it is
    /// already tracking rather than the row as it now stands. Clearing the tracker is what makes a
    /// "and then someone downloads it again" step read the database.
    /// </summary>
    protected void NewRequest() => Context.ChangeTracker.Clear();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Tree.Dispose();
        }

        base.Dispose(disposing);
    }
}
