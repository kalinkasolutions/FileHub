using Dal.Repositories.BasePaths;
using Dal.Repositories.Groups;
using Dal.Repositories.Shares;
using Dtos.BasePaths;
using Dtos.Groups;
using FileHub.BusinessLogic.Services.BasePaths;
using FileHub.BusinessLogic.Services.Files;
using FileHub.BusinessLogic.Services.Groups;
using FileHub.BusinessLogic.Services.Shares;
using Microsoft.Extensions.DependencyInjection;

namespace FileHub.IntegrationTests;

/// <summary>
/// Fixture for the browsing slice: <see cref="FileService"/> over the real base-path repository and
/// a real directory tree on disk. <see cref="BasePathService"/> comes along because a grant is what
/// makes anything visible at all, <see cref="GroupService"/> because a grant to a group is one too,
/// and <see cref="ShareService"/> because "cannot see it" has to mean "cannot share it either".
/// </summary>
public abstract class FilesTestBase : TestHostBase
{
    protected TempTree Tree { get; } = new();
    protected IFileService Files => Services.GetRequiredService<IFileService>();
    protected IBasePathService BasePaths => Services.GetRequiredService<IBasePathService>();
    protected IGroupService Groups => Services.GetRequiredService<IGroupService>();
    protected IShareService Shares => Services.GetRequiredService<IShareService>();

    protected FilesTestBase() : base(Configure)
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
