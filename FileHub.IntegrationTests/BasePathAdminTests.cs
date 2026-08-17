using Dtos.BasePaths;
using Dtos.Shares;
using Microsoft.EntityFrameworkCore;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// Administering the directories FileHub is allowed to read. The interesting half is what a delete
/// takes with it: grants and public links both hang off the base-path row, and both have to go.
/// </summary>
public sealed class BasePathAdminTests : FilesTestBase
{
    [Fact]
    public async Task Creating_a_base_path_stores_the_cleaned_absolute_path()
    {
        var sub = Tree.Dir("movies");

        var result = await BasePaths.CreateAsync(new SaveBasePathDto
        {
            Path = sub + Path.DirectorySeparatorChar,
            Name = "Movies"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(sub, result.Value.Path);
    }

    [Fact]
    public async Task Creating_a_base_path_without_a_name_falls_back_to_the_directory_name()
    {
        var sub = Tree.Dir("movies");

        var result = await BasePaths.CreateAsync(new SaveBasePathDto { Path = sub, Name = string.Empty });

        Assert.Equal("movies", result.Value.Name);
    }

    [Fact]
    public async Task Creating_a_base_path_from_a_relative_path_is_rejected()
    {
        var result = await BasePaths.CreateAsync(new SaveBasePathDto { Path = "movies", Name = "Movies" });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task Creating_a_base_path_that_is_not_on_disk_is_rejected()
    {
        var result = await BasePaths.CreateAsync(new SaveBasePathDto
        {
            Path = Path.Combine(Tree.Root, "nowhere"),
            Name = "Nowhere"
        });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.Empty(Context.BasePaths);
    }

    [Fact]
    public async Task Creating_a_base_path_from_a_file_is_rejected()
    {
        var file = Tree.File("a.txt");

        var result = await BasePaths.CreateAsync(new SaveBasePathDto { Path = file, Name = "File" });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task Creating_a_base_path_without_a_path_is_a_validation_error()
    {
        var result = await BasePaths.CreateAsync(new SaveBasePathDto { Path = string.Empty, Name = "Empty" });

        Assert.Equal(ResultCode.Validation, result.ResultCode);
        Assert.Contains(nameof(SaveBasePathDto.Path), result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Creating_the_same_base_path_twice_is_rejected()
    {
        var sub = Tree.Dir("movies");
        await CreateBasePathAsync(sub, "Movies");

        var result = await BasePaths.CreateAsync(new SaveBasePathDto { Path = sub, Name = "Again" });

        // One directory, one id: two rows would give it two access lists and two sets of links.
        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task Updating_a_base_path_repoints_and_renames_it()
    {
        var movies = await CreateBasePathAsync(Tree.Dir("movies"), "Movies");
        var music = Tree.Dir("music");

        var result = await BasePaths.UpdateAsync(movies.Id, new SaveBasePathDto { Path = music, Name = "Music" });

        Assert.True(result.IsSuccess);
        Assert.Equal(music, result.Value.Path);
        Assert.Equal("Music", result.Value.Name);
    }

    [Fact]
    public async Task Updating_a_base_path_onto_one_that_already_exists_is_rejected()
    {
        var movies = await CreateBasePathAsync(Tree.Dir("movies"), "Movies");
        var music = await CreateBasePathAsync(Tree.Dir("music"), "Music");

        var result = await BasePaths.UpdateAsync(movies.Id, new SaveBasePathDto { Path = music.Path, Name = "Clash" });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task Updating_a_base_path_to_the_path_it_already_has_is_allowed()
    {
        var movies = await CreateBasePathAsync(Tree.Dir("movies"), "Movies");

        var result = await BasePaths.UpdateAsync(movies.Id, new SaveBasePathDto { Path = movies.Path, Name = "Renamed" });

        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed", result.Value.Name);
    }

    [Fact]
    public async Task Updating_an_unknown_base_path_is_not_found()
    {
        var result = await BasePaths.UpdateAsync(
            Guid.NewGuid(), new SaveBasePathDto { Path = Tree.Root, Name = "Ghost" });

        Assert.Equal(ResultCode.NotFound, result.ResultCode);
    }

    [Fact]
    public async Task Deleting_a_base_path_removes_it_from_the_list()
    {
        var movies = await CreateBasePathAsync(Tree.Dir("movies"), "Movies");

        var result = await BasePaths.DeleteAsync(movies.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty((await BasePaths.GetAllAsync()).Value);
    }

    [Fact]
    public async Task Deleting_an_unknown_base_path_is_not_found()
    {
        Assert.Equal(ResultCode.NotFound, (await BasePaths.DeleteAsync(Guid.NewGuid())).ResultCode);
    }

    [Fact]
    public async Task Deleting_a_base_path_revokes_its_grants()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var movies = await CreateBasePathAsync(Tree.Dir("movies"), "Movies");
        await GrantAsync(movies.Id, alice.Id);

        await BasePaths.DeleteAsync(movies.Id);

        NewRequest();
        Assert.Empty(await Context.BasePathAccesses.ToListAsync());
    }

    [Fact]
    public async Task Deleting_a_base_path_revokes_the_links_into_it()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("movies/a.txt", "hello");
        var movies = await CreateBasePathAsync(Path.Combine(Tree.Root, "movies"), "Movies");
        await GrantAsync(movies.Id, alice.Id);
        await Shares.CreateAsync(alice.Id, callerIsAdmin: false, callerCanCreateShares: true, new CreateShareDto { BasePathId = movies.Id, RelativePath = "a.txt" });

        await BasePaths.DeleteAsync(movies.Id);

        // This is the whole reason a link stores (base path, relative path) rather than a resolved
        // absolute path: the cascade revokes it, nothing has to remember to.
        NewRequest();
        Assert.Empty(await Context.Shares.ToListAsync());
    }

    [Fact]
    public async Task Base_paths_are_listed_with_the_number_of_users_holding_them()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var bob = await CreateUserAsync("bob@example.com");
        var movies = await CreateBasePathAsync(Tree.Dir("movies"), "Movies");
        await GrantAsync(movies.Id, alice.Id, bob.Id);

        NewRequest();
        var result = await BasePaths.GetAllAsync();

        Assert.Equal(2, Assert.Single(result.Value).UserCount);
    }

    [Fact]
    public async Task Reading_the_users_of_an_unknown_base_path_is_not_found()
    {
        Assert.Equal(ResultCode.NotFound, (await BasePaths.GetUsersAsync(Guid.NewGuid())).ResultCode);
    }

    [Fact]
    public async Task Setting_the_users_of_an_unknown_base_path_is_not_found()
    {
        var result = await BasePaths.SetUsersAsync(Guid.NewGuid(), new SetBasePathAccessDto { UserIds = [] });

        Assert.Equal(ResultCode.NotFound, result.ResultCode);
    }

    // Left skipped on purpose: this is the behaviour the mirror method already has, and the one
    // this method does not. SetUserBasePathsAsync filters ids that match no row before saving;
    // SetUsersAsync does not, so an unknown user id reaches SQLite and comes back as an unhandled
    // DbUpdateException ("FOREIGN KEY constraint failed") — a 500, with the whole grant change lost.
    [Fact]
    public async Task Setting_the_users_of_a_base_path_drops_an_unknown_user_id()
    {
        var alice = await CreateUserAsync("alice@example.com");
        var movies = await CreateBasePathAsync(Tree.Dir("movies"), "Movies");

        var result = await BasePaths.SetUsersAsync(
            movies.Id, new SetBasePathAccessDto { UserIds = [alice.Id, Guid.NewGuid()] });

        Assert.True(result.IsSuccess);
        Assert.Equal(alice.Id, Assert.Single((await BasePaths.GetUsersAsync(movies.Id)).Value));
    }
}
