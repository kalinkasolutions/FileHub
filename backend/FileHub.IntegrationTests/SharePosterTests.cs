using Entities.Media;
using Entities.Paths;
using FileHub.BusinessLogic.Media;
using FileHub.BusinessLogic.Services.Shares;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// The cover a link preview shows: the cached poster of the title folder the link sits in, and
/// nothing else — never a fresh look-up, and never for a link the caller could not resolve.
/// </summary>
public sealed class SharePosterTests : SharesTestBase
{
    private FakePosterStore Posters => (FakePosterStore)Services.GetRequiredService<IPosterStore>();

    private MediaRefreshQueue Queue => Services.GetRequiredService<MediaRefreshQueue>();
    private MediaRefreshWorker Worker => Services.GetRequiredService<MediaRefreshWorker>();

    private FakeLookupProvider Tmdb => Services.GetServices<IMediaLookupProvider>()
        .OfType<FakeLookupProvider>()
        .Single(p => p.Kind == BasePathKind.Media);

    [Fact]
    public async Task A_link_to_a_title_folder_shows_its_cached_poster()
    {
        var (aliceId, basePathId) = await MediaDiskAsync("media");
        var info = await CachePosterAsync(basePathId, "Heat (1995)");
        var share = await ShareAsync(aliceId, basePathId, "Heat (1995)");

        var poster = await FindPosterAsync(share.Id);

        Assert.True(poster.IsSuccess, poster.ErrorMessage);
        Assert.Equal(Posters.PathOf(info.Id), poster.Value.FullPath);
        Assert.Equal(info.FetchedAt, poster.Value.FetchedAt);
    }

    [Fact]
    public async Task A_link_to_a_file_inside_a_title_folder_shows_the_titles_poster()
    {
        var (aliceId, basePathId) = await MediaDiskAsync("games");
        var info = await CachePosterAsync(basePathId, "Heat (1995)");
        var share = await ShareAsync(aliceId, basePathId, "Heat (1995)/heat.mkv");

        var poster = await FindPosterAsync(share.Id);

        Assert.Equal(Posters.PathOf(info.Id), poster.Value.FullPath);
    }

    [Fact]
    public async Task A_link_on_an_unmarked_disk_has_no_poster_even_when_one_is_cached()
    {
        var (aliceId, basePathId) = await MediaDiskAsync("none");
        await CachePosterAsync(basePathId, "Heat (1995)");
        var share = await ShareAsync(aliceId, basePathId, "Heat (1995)");

        Assert.Equal(ResultCode.NotFound, (await FindPosterAsync(share.Id)).ResultCode);
    }

    [Fact]
    public async Task A_title_nothing_matched_has_no_poster()
    {
        var (aliceId, basePathId) = await MediaDiskAsync("media");
        Tmdb.Answer = null;
        var share = await ShareAsync(aliceId, basePathId, "Heat (1995)");

        Assert.Equal(ResultCode.NotFound, (await FindPosterAsync(share.Id)).ResultCode);
    }

    [Fact]
    public async Task Creating_a_link_on_a_media_disk_looks_the_title_up_so_the_first_preview_has_a_cover()
    {
        var (aliceId, basePathId) = await MediaDiskAsync("media");
        Tmdb.Answer = FakeLookupProvider.Movie("Heat", 1995);

        var share = await ShareAsync(aliceId, basePathId, "Heat (1995)/heat.mkv");

        Assert.Equal("Heat", Assert.Single(Tmdb.Calls).Title);
        var poster = await FindPosterAsync(share.Id);
        Assert.True(poster.IsSuccess, poster.ErrorMessage);
    }

    [Fact]
    public async Task Creating_a_link_into_a_title_already_looked_up_does_not_ask_again()
    {
        var (aliceId, basePathId) = await MediaDiskAsync("media");
        await CachePosterAsync(basePathId, "Heat (1995)");

        await ShareAsync(aliceId, basePathId, "Heat (1995)");

        Assert.Empty(Tmdb.Calls);
    }

    [Fact]
    public async Task Creating_a_link_on_an_unmarked_disk_looks_nothing_up()
    {
        var (aliceId, basePathId) = await MediaDiskAsync("none");

        await ShareAsync(aliceId, basePathId, "Heat (1995)");

        Assert.Empty(Tmdb.Calls);
    }

    [Fact]
    public async Task Creating_a_link_to_the_disk_itself_looks_nothing_up()
    {
        var (aliceId, basePathId) = await MediaDiskAsync("media");

        await ShareAsync(aliceId, basePathId, string.Empty);

        Assert.Empty(Tmdb.Calls);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(InvalidOperationException))]
    public async Task A_look_up_that_fails_still_creates_the_link(Type failure)
    {
        var (aliceId, basePathId) = await MediaDiskAsync("media");
        Tmdb.Failure = (Exception)Activator.CreateInstance(failure)!;

        var share = await ShareAsync(aliceId, basePathId, "Heat (1995)");

        NewRequest();
        Assert.True((await Shares.ResolvePublicAsync(share.Id, callerId: null, callerIsAdmin: false)).IsSuccess);
        Assert.Equal(ResultCode.NotFound, (await FindPosterAsync(share.Id)).ResultCode);
    }

    [Fact]
    public async Task A_link_to_the_disk_itself_has_no_poster()
    {
        var (aliceId, basePathId) = await MediaDiskAsync("media");
        await CachePosterAsync(basePathId, "Heat (1995)");
        var share = await ShareAsync(aliceId, basePathId, string.Empty);

        Assert.Equal(ResultCode.NotFound, (await FindPosterAsync(share.Id)).ResultCode);
    }

    [Fact]
    public async Task A_row_whose_poster_file_is_gone_has_no_poster()
    {
        var (aliceId, basePathId) = await MediaDiskAsync("media");
        var info = await CachePosterAsync(basePathId, "Heat (1995)");
        Posters.Delete(info.Id);
        var share = await ShareAsync(aliceId, basePathId, "Heat (1995)");

        Assert.Equal(ResultCode.NotFound, (await FindPosterAsync(share.Id)).ResultCode);
    }

    [Fact]
    public async Task A_group_only_link_does_not_resolve_for_a_stranger_so_there_is_no_poster_to_ask_for()
    {
        var admin = await CreateUserAsync("admin@example.com");
        var (_, basePathId) = await MediaDiskAsync("media");
        await CachePosterAsync(basePathId, "Heat (1995)");
        var family = await CreateGroupAsync("Family");
        var share = await ShareAsync(admin.Id, basePathId, "Heat (1995)", audienceGroupId: family.Id, callerIsAdmin: true);

        NewRequest();
        var resolved = await Shares.ResolvePublicAsync(share.Id, callerId: null, callerIsAdmin: false);

        // The poster route and the preview page both start here, so the audience gates the cover too.
        AssertPublicFailure(resolved);
    }

    [Fact]
    public async Task The_landing_page_gets_the_titles_info_without_the_folder_it_came_from()
    {
        var (aliceId, basePathId) = await MediaDiskAsync("media");
        await CachePosterAsync(basePathId, "Heat (1995)");
        var share = await ShareAsync(aliceId, basePathId, "Heat (1995)/heat.mkv");

        NewRequest();
        var resolved = await Shares.ResolvePublicAsync(share.Id, callerId: null, callerIsAdmin: false);
        var title = await Shares.FindTitleAsync(resolved.Value);

        Assert.True(title.IsSuccess, title.ErrorMessage);
        Assert.Equal("Heat", title.Value.Title);
        Assert.Equal(string.Empty, title.Value.Query);
        Assert.Equal(Guid.Empty, title.Value.Id);
    }

    [Fact]
    public async Task The_landing_page_gets_no_info_for_a_miss()
    {
        var (aliceId, basePathId) = await MediaDiskAsync("media");
        await CachePosterAsync(basePathId, "Heat (1995)", found: false);
        var share = await ShareAsync(aliceId, basePathId, "Heat (1995)");

        NewRequest();
        var resolved = await Shares.ResolvePublicAsync(share.Id, callerId: null, callerIsAdmin: false);

        Assert.Equal(ResultCode.NotFound, (await Shares.FindTitleAsync(resolved.Value)).ResultCode);
    }

    // ---- refreshing from a preview ----

    [Fact]
    public async Task A_preview_with_no_row_queues_a_look_up_and_the_next_preview_has_the_cover()
    {
        var (aliceId, basePathId) = await MediaDiskAsync("media");
        var share = await ShareWithoutWarmingAsync(aliceId, basePathId, "Heat (1995)");
        Tmdb.Answer = FakeLookupProvider.Movie("Heat", 1995);

        Assert.Equal(ResultCode.NotFound, (await FindPosterAsync(share.Id)).ResultCode);
        Assert.Empty(Tmdb.Calls);

        await RunQueuedAsync();

        Assert.Single(Tmdb.Calls);
        Assert.True((await FindPosterAsync(share.Id)).IsSuccess);
    }

    [Fact]
    public async Task A_preview_of_a_stale_cover_still_shows_it_and_queues_a_refresh()
    {
        var (aliceId, basePathId) = await MediaDiskAsync("media");
        var share = await ShareWithoutWarmingAsync(aliceId, basePathId, "Heat (1995)");
        var stale = await CachePosterAsync(basePathId, "Heat (1995)", ageInDays: 200);
        Tmdb.Answer = FakeLookupProvider.Movie("Heat", 1995);

        var poster = await FindPosterAsync(share.Id);
        Assert.Equal(stale.FetchedAt, poster.Value.FetchedAt);

        await RunQueuedAsync();

        Assert.Single(Tmdb.Calls);
        Assert.True((await FindPosterAsync(share.Id)).Value.FetchedAt > stale.FetchedAt);
    }

    [Fact]
    public async Task A_miss_is_never_asked_again_from_a_preview_however_old()
    {
        var (aliceId, basePathId) = await MediaDiskAsync("media");
        var share = await ShareWithoutWarmingAsync(aliceId, basePathId, "Heat (1995)");
        await CachePosterAsync(basePathId, "Heat (1995)", found: false, ageInDays: 400);

        await FindPosterAsync(share.Id);

        Assert.False(Queue.TryRead(out _));

        // Even queued by hand, the service asks the rule again and leaves it alone.
        await Worker.RefreshAsync(new MediaRefreshRequest(basePathId, "Heat (1995)"), CancellationToken.None);
        Assert.Empty(Tmdb.Calls);
    }

    [Fact]
    public async Task A_fresh_cover_queues_nothing()
    {
        var (aliceId, basePathId) = await MediaDiskAsync("media");
        await CachePosterAsync(basePathId, "Heat (1995)");
        var share = await ShareAsync(aliceId, basePathId, "Heat (1995)");

        await FindPosterAsync(share.Id);

        Assert.False(Queue.TryRead(out _));
    }

    [Fact]
    public async Task Many_previews_of_one_title_queue_one_look_up()
    {
        var (aliceId, basePathId) = await MediaDiskAsync("media");
        var share = await ShareWithoutWarmingAsync(aliceId, basePathId, "Heat (1995)");

        await FindPosterAsync(share.Id);
        await FindPosterAsync(share.Id);
        await FindPosterAsync(share.Id);

        Assert.True(Queue.TryRead(out _));
        Assert.False(Queue.TryRead(out _));
    }

    [Fact]
    public async Task A_failed_refresh_frees_the_title_to_be_queued_again()
    {
        var (aliceId, basePathId) = await MediaDiskAsync("media");
        var share = await ShareWithoutWarmingAsync(aliceId, basePathId, "Heat (1995)");
        Tmdb.Failure = new InvalidOperationException();

        await FindPosterAsync(share.Id);
        await RunQueuedAsync();
        await FindPosterAsync(share.Id);

        Assert.True(Queue.TryRead(out _));
    }

    /// <summary>A link whose title was never looked up — the source was down when it was made.</summary>
    private async Task<Dtos.Shares.ShareDto> ShareWithoutWarmingAsync(Guid userId, Guid basePathId, string relativePath)
    {
        Tmdb.IsConfigured = false;
        var share = await ShareAsync(userId, basePathId, relativePath);
        Tmdb.IsConfigured = true;
        return share;
    }

    private async Task RunQueuedAsync()
    {
        while (Queue.TryRead(out var request))
        {
            await Worker.RefreshAsync(request, CancellationToken.None);
        }
    }

    private async Task<(Guid AliceId, Guid BasePathId)> MediaDiskAsync(string kind)
    {
        var alice = await CreateUserAsync("alice@example.com");
        Tree.File("Heat (1995)/heat.mkv");
        var basePath = await CreateBasePathAsync(Tree.Root, "Films", kind);
        await GrantAsync(basePath.Id, alice.Id);
        return (alice.Id, basePath.Id);
    }

    private async Task<MediaInfo> CachePosterAsync(Guid basePathId, string folder, bool found = true, int ageInDays = 1)
    {
        var info = new MediaInfo
        {
            Id = Guid.NewGuid(),
            BasePathId = basePathId,
            RelativePath = folder,
            Query = "Heat",
            Found = found,
            Source = "tmdb",
            MediaType = "movie",
            ExternalUrl = string.Empty,
            Title = "Heat",
            Overview = string.Empty,
            Genres = string.Empty,
            Platforms = string.Empty,
            HasPoster = found,
            // Fresh by default, so creating a link reads this row rather than asking the source again.
            FetchedAt = DateTime.UtcNow.AddDays(-ageInDays)
        };
        Context.MediaInfos.Add(info);
        await Context.SaveChangesAsync();
        if (found)
        {
            await Posters.SaveAsync(info.Id, [1, 2, 3], CancellationToken.None);
        }

        return info;
    }

    private async Task<OperationResult<PublicSharePoster>> FindPosterAsync(Guid shareId)
    {
        NewRequest();
        var resolved = await Shares.ResolvePublicAsync(shareId, callerId: null, callerIsAdmin: false);
        Assert.True(resolved.IsSuccess, resolved.ErrorMessage);
        return await Shares.FindPosterAsync(resolved.Value);
    }
}
