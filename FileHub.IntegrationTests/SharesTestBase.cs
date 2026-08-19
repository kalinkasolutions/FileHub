using Dal.Repositories.Admin;
using Dal.Repositories.BasePaths;
using Dal.Repositories.Groups;
using Dal.Repositories.Shares;
using Dtos.BasePaths;
using Dtos.Groups;
using Dtos.Shares;
using FileHub.BusinessLogic.Services.Admin;
using FileHub.BusinessLogic.Services.BasePaths;
using FileHub.BusinessLogic.Services.Files;
using FileHub.BusinessLogic.Services.Groups;
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
    protected IGroupService Groups => Services.GetRequiredService<IGroupService>();
    protected IFileService Files => Services.GetRequiredService<IFileService>();

    /// <summary>
    /// The admin user service, because taking the CreateShares role away is one of the things that
    /// revokes a link — so proving it does needs both halves in one fixture.
    /// </summary>
    protected IUserAdminService Users => Services.GetRequiredService<IUserAdminService>();

    protected SharesTestBase() : base(Configure)
    {
    }

    private static void Configure(IServiceCollection services)
    {
        services.AddScoped<IBasePathRepository, BasePathRepository>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IShareRepository, ShareRepository>();
        services.AddScoped<IBasePathService, BasePathService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IShareService, ShareService>();
        services.AddScoped<IUserAdminRepository, UserAdminRepository>();
        services.AddScoped<IUserAdminService, UserAdminService>();
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

    /// <summary>Creates a group with the given members, through the service an admin would use.</summary>
    protected async Task<GroupDto> CreateGroupAsync(string name, params Guid[] memberIds)
    {
        var created = await Groups.CreateAsync(new SaveGroupDto { Name = name });
        Assert.True(created.IsSuccess, created.ErrorMessage);

        if (memberIds.Length > 0)
        {
            var members = await Groups.SetMembersAsync(
                created.Value.Id, new SetGroupMembersDto { UserIds = [.. memberIds] });
            Assert.True(members.IsSuccess, members.ErrorMessage);
        }

        return created.Value;
    }

    /// <summary>Replaces the groups granted a base path — an id left out is a revocation.</summary>
    protected async Task GrantToGroupsAsync(Guid basePathId, params Guid[] groupIds)
    {
        var result = await BasePaths.SetGroupsAsync(basePathId, new SetBasePathGroupsDto { GroupIds = [.. groupIds] });
        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    /// <summary>
    /// Creates a link and asserts it was created, so a test can get straight to the link. Aiming one
    /// at a group needs <paramref name="callerIsAdmin"/>: the audience is admin-only.
    /// </summary>
    protected async Task<ShareDto> ShareAsync(
        Guid userId,
        Guid basePathId,
        string relativePath,
        int maxDownloads = 0,
        Guid? audienceGroupId = null,
        bool callerIsAdmin = false)
    {
        var result = await Shares.CreateAsync(
            userId,
            callerIsAdmin,
            callerCanCreateShares: true,
            new CreateShareDto
            {
                BasePathId = basePathId,
                RelativePath = relativePath,
                MaxDownloadCount = maxDownloads,
                AudienceGroupId = audienceGroupId
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
