using Dal.Repositories.BasePaths;
using Dal.Repositories.Shares;
using Dtos.BasePaths;
using Dtos.Shares;
using FileHub.BusinessLogic.Services.BasePaths;
using FileHub.BusinessLogic.Services.Files;
using FileHub.BusinessLogic.Services.Shares;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// Fixture for the public-link slice: <see cref="ShareService"/> over the real repositories and a
/// real directory tree, so the size a link is created with is measured off the disk and the public
/// resolve re-runs the sandbox for real. <see cref="BasePathService"/> comes along because a link
/// is created against a granted base path and revoked by deleting one.
/// </summary>
public abstract class SharesTestBase : TestHostBase
{
    protected TempTree Tree { get; } = new();
    protected IShareService Shares => Services.GetRequiredService<IShareService>();
    protected IBasePathService BasePaths => Services.GetRequiredService<IBasePathService>();
    protected IFileService Files => Services.GetRequiredService<IFileService>();

    protected SharesTestBase() : base(Configure)
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

    protected async Task<BasePathDto> CreateBasePathAsync(string path, string name = "Media")
    {
        var result = await BasePaths.CreateAsync(new SaveBasePathDto { Path = path, Name = name });
        Assert.True(result.IsSuccess, result.ErrorMessage);
        return result.Value;
    }

    protected async Task GrantAsync(Guid basePathId, params Guid[] userIds)
    {
        var result = await BasePaths.SetUsersAsync(basePathId, new SetBasePathAccessDto { UserIds = [.. userIds] });
        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    /// <summary>Creates a link and asserts it was created, so a test can get straight to the link.</summary>
    protected async Task<ShareDto> ShareAsync(Guid userId, Guid basePathId, string relativePath, int maxDownloads = 0)
    {
        var result = await Shares.CreateAsync(userId, new CreateShareDto
        {
            BasePathId = basePathId,
            RelativePath = relativePath,
            MaxDownloadCount = maxDownloads
        });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        return result.Value;
    }

    /// <summary>
    /// Stands in for the request boundary — see <see cref="FilesTestBase.NewRequest"/>. It matters
    /// more here than anywhere else: the download counter is incremented straight in the database,
    /// so the tracked entity a test is holding does not see it.
    /// </summary>
    protected void NewRequest() => Context.ChangeTracker.Clear();

    protected static void AssertPublicFailure(OperationResult<ResolvedShare> result)
    {
        // Unknown id, exhausted limit and a vanished target all answer the same, so the response
        // cannot be used to tell which links exist.
        Assert.Equal(ResultCode.NotFound, result.ResultCode);
        Assert.Equal("Share not found", result.ErrorMessage);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Tree.Dispose();
        }

        base.Dispose(disposing);
    }
}
