using Dal.Repositories.Media;
using Dtos.BasePaths;
using Dtos.Files;
using Entities.Paths;
using FileHub.BusinessLogic.Media;
using FileHub.BusinessLogic.Services.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// The info behind a folder's info button: looked up once, cached from then on, answered on the
/// base path's access rules, and forgotten with the disk. The sources are faked; what is under
/// test is everything the service does around them.
/// </summary>
public sealed class MediaInfoTests : FilesTestBase
{
    private readonly FakeLookupProvider m_tmdb;
    private readonly FakeLookupProvider m_igdb;
    private readonly FakePosterFetcher m_posters;

    private IMediaInfoService Info => Services.GetRequiredService<IMediaInfoService>();
    private FakePosterStore Store => (FakePosterStore)Services.GetRequiredService<IPosterStore>();

    public MediaInfoTests() : this(new FakeLookupProvider(BasePathKind.Media), new FakeLookupProvider(BasePathKind.Games), new FakePosterFetcher())
    {
    }

    // The fakes are created before the base constructor runs and handed to it through the
    // delegate, so the test holds the same instances the service was built with.
    private MediaInfoTests(FakeLookupProvider tmdb, FakeLookupProvider igdb, FakePosterFetcher posters) : base(services =>
    {
        services.AddSingleton<IMediaLookupProvider>(tmdb);
        services.AddSingleton<IMediaLookupProvider>(igdb);
        services.AddSingleton<IPosterFetcher>(posters);
        services.AddScoped<IMediaInfoService, MediaInfoService>();
    })
    {
        m_tmdb = tmdb;
        m_igdb = igdb;
        m_posters = posters;
    }

    [Fact]
    public async Task A_folder_under_a_media_disk_is_looked_up_once_and_cached()
    {
        var (alice, basePath) = await MediaDiskAsync("Dune Part Two (2024)");
        m_tmdb.Answer = FakeLookupProvider.Movie("Dune: Part Two", 2024);

        var first = await Info.GetAsync(alice, false, basePath, "Dune Part Two (2024)", refresh: false, CancellationToken.None);
        NewRequest();
        var second = await Info.GetAsync(alice, false, basePath, "Dune Part Two (2024)", refresh: false, CancellationToken.None);

        Assert.True(first.IsSuccess, first.ErrorMessage);
        Assert.True(first.Value.Found);
        Assert.Equal("Dune: Part Two", first.Value.Title);
        Assert.Equal(2024, first.Value.Year);
        Assert.Equal(["Science Fiction", "Adventure"], first.Value.Genres);
        Assert.True(first.Value.HasPoster);
        Assert.Equal(first.Value.Id, second.Value.Id);
        Assert.Single(m_tmdb.Calls);
        Assert.Single(m_posters.Fetched);
    }

    [Fact]
    public async Task The_folder_name_is_cleaned_before_it_is_searched_for()
    {
        var (alice, basePath) = await MediaDiskAsync("Dune.Part.Two.2024.1080p.BluRay.x264-GROUP");
        m_tmdb.Answer = FakeLookupProvider.Movie("Dune: Part Two", 2024);

        await Info.GetAsync(alice, false, basePath, "Dune.Part.Two.2024.1080p.BluRay.x264-GROUP", refresh: false, CancellationToken.None);

        var asked = Assert.Single(m_tmdb.Calls);
        Assert.Equal("Dune Part Two", asked.Title);
        Assert.Equal(2024, asked.Year);
    }

    [Fact]
    public async Task A_miss_is_cached_too_so_the_same_search_does_not_run_on_every_open()
    {
        var (alice, basePath) = await MediaDiskAsync("Home Videos 2019");

        var first = await Info.GetAsync(alice, false, basePath, "Home Videos 2019", refresh: false, CancellationToken.None);
        NewRequest();
        var second = await Info.GetAsync(alice, false, basePath, "Home Videos 2019", refresh: false, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.False(first.Value.Found);
        Assert.Equal("Home Videos", first.Value.Query);
        Assert.False(second.Value.Found);
        Assert.Single(m_tmdb.Calls);
    }

    [Fact]
    public async Task A_games_disk_asks_IGDB_not_TMDb()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.Dir("Cyberpunk 2077");
        var basePath = await CreateBasePathAsync(Tree.Root, "Games", kind: "games");
        await GrantAsync(basePath.Id, alice.Id);
        m_igdb.Answer = FakeLookupProvider.Movie("Cyberpunk 2077", 2020) with { MediaType = "game", Platforms = ["PC", "PS5"] };

        var result = await Info.GetAsync(alice.Id, false, basePath.Id, "Cyberpunk 2077", refresh: false, CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal("game", result.Value.MediaType);
        Assert.Equal(["PC", "PS5"], result.Value.Platforms);
        Assert.Equal("Cyberpunk 2077", Assert.Single(m_igdb.Calls).Title);
        Assert.Null(Assert.Single(m_igdb.Calls).Year);
        Assert.Empty(m_tmdb.Calls);
    }

    [Fact]
    public async Task A_user_without_access_gets_not_found_and_the_source_is_never_asked()
    {
        var (_, basePath) = await MediaDiskAsync("Dune Part Two (2024)");
        var mallory = await CreateUserAsync("mallory@example.com");

        var result = await Info.GetAsync(mallory.Id, false, basePath, "Dune Part Two (2024)", refresh: false, CancellationToken.None);

        Assert.Equal(ResultCode.NotFound, result.ResultCode);
        Assert.Empty(m_tmdb.Calls);
    }

    [Fact]
    public async Task An_admin_reaches_it_without_a_grant()
    {
        var (_, basePath) = await MediaDiskAsync("Dune Part Two (2024)");
        var admin = await CreateUserAsync("admin@example.com", roles: Roles.Admin);
        m_tmdb.Answer = FakeLookupProvider.Movie("Dune: Part Two", 2024);

        var result = await Info.GetAsync(admin.Id, callerIsAdmin: true, basePath, "Dune Part Two (2024)", refresh: false, CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    [Fact]
    public async Task A_disk_with_no_kind_has_nothing_to_look_up()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.Dir("Dune Part Two (2024)");
        var basePath = await CreateBasePathAsync(Tree.Root, "Files");
        await GrantAsync(basePath.Id, alice.Id);

        var result = await Info.GetAsync(alice.Id, false, basePath.Id, "Dune Part Two (2024)", refresh: false, CancellationToken.None);

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.Empty(m_tmdb.Calls);
    }

    [Fact]
    public async Task Only_a_folder_directly_under_the_disk_is_a_title()
    {
        var (alice, basePath) = await MediaDiskAsync("Scrubs (2001)/season_01");

        var nested = await Info.GetAsync(alice, false, basePath, "Scrubs (2001)/season_01", refresh: false, CancellationToken.None);
        var root = await Info.GetAsync(alice, false, basePath, string.Empty, refresh: false, CancellationToken.None);
        var missing = await Info.GetAsync(alice, false, basePath, "Nowhere (1999)", refresh: false, CancellationToken.None);

        Assert.Equal(ResultCode.BadRequest, nested.ResultCode);
        Assert.Equal(ResultCode.BadRequest, root.ResultCode);
        Assert.Equal(ResultCode.NotFound, missing.ResultCode);
        Assert.Empty(m_tmdb.Calls);
    }

    [Fact]
    public async Task The_listing_marks_the_top_level_folders_of_a_marked_disk()
    {
        var (alice, basePath) = await MediaDiskAsync("Scrubs (2001)/season_01");
        Tree.File("notes.txt");

        var top = await Files.NavigateAsync(alice, false, new NavigateDto { BasePathId = basePath, Path = string.Empty });
        var below = await Files.NavigateAsync(alice, false, new NavigateDto { BasePathId = basePath, Path = "Scrubs (2001)" });

        Assert.True(top.Value.Entries.Single(e => e.Name == "Scrubs (2001)").HasInfo);
        Assert.False(top.Value.Entries.Single(e => e.Name == "notes.txt").HasInfo);
        Assert.False(Assert.Single(below.Value.Entries).HasInfo);
    }

    [Fact]
    public async Task A_plain_disk_marks_nothing()
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.Dir("Scrubs (2001)");
        var basePath = await CreateBasePathAsync(Tree.Root, "Files");
        await GrantAsync(basePath.Id, alice.Id);

        var top = await Files.NavigateAsync(alice.Id, false, new NavigateDto { BasePathId = basePath.Id, Path = string.Empty });

        Assert.False(Assert.Single(top.Value.Entries).HasInfo);
    }

    [Fact]
    public async Task A_source_that_does_not_answer_is_a_bad_gateway_and_nothing_is_cached()
    {
        var (alice, basePath) = await MediaDiskAsync("Dune Part Two (2024)");
        m_tmdb.Failure = new HttpRequestException("connection refused");

        var failed = await Info.GetAsync(alice, false, basePath, "Dune Part Two (2024)", refresh: false, CancellationToken.None);
        m_tmdb.Failure = null;
        m_tmdb.Answer = FakeLookupProvider.Movie("Dune: Part Two", 2024);
        NewRequest();
        var retried = await Info.GetAsync(alice, false, basePath, "Dune Part Two (2024)", refresh: false, CancellationToken.None);

        Assert.Equal(ResultCode.BadGateway, failed.ResultCode);
        Assert.True(retried.IsSuccess);
        Assert.Equal(2, m_tmdb.Calls.Count);
    }

    [Fact]
    public async Task A_rejected_key_is_reported_in_the_sources_own_words()
    {
        var (alice, basePath) = await MediaDiskAsync("Dune Part Two (2024)");
        m_tmdb.Failure = new MediaLookupException("TMDb rejected the configured API key");

        var result = await Info.GetAsync(alice, false, basePath, "Dune Part Two (2024)", refresh: false, CancellationToken.None);

        Assert.Equal(ResultCode.BadGateway, result.ResultCode);
        Assert.Equal("TMDb rejected the configured API key", result.ErrorMessage);
    }

    [Fact]
    public async Task An_unconfigured_source_says_so_rather_than_failing()
    {
        var (alice, basePath) = await MediaDiskAsync("Dune Part Two (2024)");
        m_tmdb.IsConfigured = false;

        var result = await Info.GetAsync(alice, false, basePath, "Dune Part Two (2024)", refresh: false, CancellationToken.None);

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
        Assert.Contains("TMDb is not configured", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(m_tmdb.Calls);
    }

    [Fact]
    public async Task A_refresh_asks_again_and_keeps_the_same_row()
    {
        var (alice, basePath) = await MediaDiskAsync("Dune Part Two (2024)");
        m_tmdb.Answer = FakeLookupProvider.Movie("Dune: Part Two", 2024);
        var first = await Info.GetAsync(alice, false, basePath, "Dune Part Two (2024)", refresh: false, CancellationToken.None);

        m_tmdb.Answer = FakeLookupProvider.Movie("Dune: Part Two", 2024) with { Rating = 8.5 };
        NewRequest();
        var refreshed = await Info.GetAsync(alice, false, basePath, "Dune Part Two (2024)", refresh: true, CancellationToken.None);

        Assert.Equal(first.Value.Id, refreshed.Value.Id);
        Assert.Equal(8.5, refreshed.Value.Rating);
        Assert.Equal(2, m_tmdb.Calls.Count);
        Assert.Equal(1, await Context.MediaInfos.CountAsync());
    }

    [Fact]
    public async Task An_answer_older_than_six_months_is_fetched_again()
    {
        var (alice, basePath) = await MediaDiskAsync("Dune Part Two (2024)");
        m_tmdb.Answer = FakeLookupProvider.Movie("Dune: Part Two", 2024);
        await Info.GetAsync(alice, false, basePath, "Dune Part Two (2024)", refresh: false, CancellationToken.None);

        var row = await Context.MediaInfos.SingleAsync();
        row.FetchedAt = DateTime.UtcNow.AddDays(-200);
        await Context.SaveChangesAsync();
        NewRequest();

        await Info.GetAsync(alice, false, basePath, "Dune Part Two (2024)", refresh: false, CancellationToken.None);

        Assert.Equal(2, m_tmdb.Calls.Count);
    }

    [Fact]
    public async Task A_poster_that_cannot_be_fetched_is_a_blank_not_a_failure()
    {
        var (alice, basePath) = await MediaDiskAsync("Dune Part Two (2024)");
        m_tmdb.Answer = FakeLookupProvider.Movie("Dune: Part Two", 2024);
        m_posters.Available = false;

        var result = await Info.GetAsync(alice, false, basePath, "Dune Part Two (2024)", refresh: false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Found);
        Assert.False(result.Value.HasPoster);
        Assert.Empty(Store.Files);
    }

    [Fact]
    public async Task The_poster_is_answered_on_the_disks_access_rules()
    {
        var (alice, basePath) = await MediaDiskAsync("Dune Part Two (2024)");
        var mallory = await CreateUserAsync("mallory@example.com");
        m_tmdb.Answer = FakeLookupProvider.Movie("Dune: Part Two", 2024);
        var info = await Info.GetAsync(alice, false, basePath, "Dune Part Two (2024)", refresh: false, CancellationToken.None);

        var own = await Info.GetPosterPathAsync(alice, false, info.Value.Id);
        var other = await Info.GetPosterPathAsync(mallory.Id, false, info.Value.Id);
        var unknown = await Info.GetPosterPathAsync(alice, false, Guid.NewGuid());

        Assert.True(own.IsSuccess);
        Assert.Equal(Store.PathOf(info.Value.Id), own.Value);
        Assert.Equal(ResultCode.NotFound, other.ResultCode);
        Assert.Equal(ResultCode.NotFound, unknown.ResultCode);
    }

    [Fact]
    public async Task Changing_the_disks_kind_forgets_its_answers_and_their_posters()
    {
        var (alice, basePath) = await MediaDiskAsync("Dune Part Two (2024)");
        m_tmdb.Answer = FakeLookupProvider.Movie("Dune: Part Two", 2024);
        var info = await Info.GetAsync(alice, false, basePath, "Dune Part Two (2024)", refresh: false, CancellationToken.None);
        NewRequest();

        var updated = await BasePaths.UpdateAsync(basePath, new SaveBasePathDto { Path = Tree.Root, Name = "Games", Kind = "games" });

        Assert.True(updated.IsSuccess, updated.ErrorMessage);
        Assert.Equal("games", updated.Value.Kind);
        Assert.Empty(await Context.MediaInfos.ToListAsync());
        Assert.False(Store.Exists(info.Value.Id));
    }

    [Fact]
    public async Task Renaming_the_disk_keeps_its_answers()
    {
        var (alice, basePath) = await MediaDiskAsync("Dune Part Two (2024)");
        m_tmdb.Answer = FakeLookupProvider.Movie("Dune: Part Two", 2024);
        await Info.GetAsync(alice, false, basePath, "Dune Part Two (2024)", refresh: false, CancellationToken.None);
        NewRequest();

        await BasePaths.UpdateAsync(basePath, new SaveBasePathDto { Path = Tree.Root, Name = "Films", Kind = "media" });

        Assert.Equal(1, await Context.MediaInfos.CountAsync());
    }

    [Fact]
    public async Task Deleting_the_disk_takes_the_answers_and_the_posters_with_it()
    {
        var (alice, basePath) = await MediaDiskAsync("Dune Part Two (2024)");
        m_tmdb.Answer = FakeLookupProvider.Movie("Dune: Part Two", 2024);
        var info = await Info.GetAsync(alice, false, basePath, "Dune Part Two (2024)", refresh: false, CancellationToken.None);
        NewRequest();

        var deleted = await BasePaths.DeleteAsync(basePath);

        Assert.True(deleted.IsSuccess, deleted.ErrorMessage);
        Assert.Empty(await Context.MediaInfos.ToListAsync());
        Assert.False(Store.Exists(info.Value.Id));
    }

    [Fact]
    public async Task A_kind_that_is_not_one_of_the_three_words_is_refused()
    {
        var result = await BasePaths.CreateAsync(new SaveBasePathDto { Path = Tree.Root, Name = "Odd", Kind = "music" });

        Assert.Equal(ResultCode.BadRequest, result.ResultCode);
    }

    [Fact]
    public async Task A_base_path_reports_its_kind_and_defaults_to_none()
    {
        var plain = await CreateBasePathAsync(Tree.Dir("plain"), "Plain");
        var media = await CreateBasePathAsync(Tree.Dir("media"), "Media", kind: "Media");

        Assert.Equal("none", plain.Kind);
        Assert.Equal("media", media.Kind);
        Assert.Equal(["media", "none"], (await BasePaths.GetAllAsync()).Value.Select(p => p.Kind));
    }

    /// <summary>A media disk with one title folder, granted to alice. Returns her id and the disk's.</summary>
    private async Task<(Guid UserId, Guid BasePathId)> MediaDiskAsync(string folder)
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.Dir(folder);
        var basePath = await CreateBasePathAsync(Tree.Root, "Movies", kind: "media");
        await GrantAsync(basePath.Id, alice.Id);
        return (alice.Id, basePath.Id);
    }
}
