using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// The doorbell behind the admin log screen's live view, and the one rule that keeps that view from
/// feeding itself.
///
/// <para>
/// These are the parts of the push path that live in <c>FileHub.Shared</c> and are therefore
/// testable here. The hub, the SignalR connection, the Serilog sink that rings the bell and the
/// background broadcaster are all in <c>FileHub.Api</c>, which this project deliberately does not
/// reference — so they stay in the "needs an HTTP-level test" list in CLAUDE.md. What is covered
/// here is the coalescing and the loop guard, which is where the behaviour actually lives and where
/// both of the bugs were.
/// </para>
/// </summary>
public sealed class LogSignalTests
{
    [Fact]
    public async Task A_ring_wakes_a_waiter()
    {
        var signal = new LogChangeSignal();

        signal.Ring();

        // Already rung, so this completes without anything else having to happen.
        await signal.WaitAsync(TestTimeout());
    }

    [Fact]
    public async Task A_burst_of_rings_is_one_notification()
    {
        var signal = new LogChangeSignal();

        // One request can write a dozen lines — the audit line, the request line, EF's warnings.
        // The screen should be told once and fetch once, not a dozen times.
        for (var i = 0; i < 200; i++)
        {
            signal.Ring();
        }

        await signal.WaitAsync(TestTimeout());

        await AssertNothingWaiting(signal);
    }

    [Fact]
    public void Ringing_never_blocks_and_never_throws_with_nobody_listening()
    {
        var signal = new LogChangeSignal();

        // Ring is called from inside the logging pipeline, on whatever thread happened to log. If
        // it could block or throw when the slot is full, writing a log line could stall or break
        // the request that wrote it.
        var exception = Record.Exception(() =>
        {
            for (var i = 0; i < 10_000; i++)
            {
                signal.Ring();
            }
        });

        Assert.Null(exception);
    }

    [Fact]
    public async Task A_second_ring_after_the_first_was_taken_notifies_again()
    {
        var signal = new LogChangeSignal();

        signal.Ring();
        await signal.WaitAsync(TestTimeout());

        // Coalescing must not mean "one notification ever": the slot frees up once it is read.
        signal.Ring();
        await signal.WaitAsync(TestTimeout());
    }

    [Theory]
    [InlineData("/api/admin/logs")]
    [InlineData("/api/admin/logs/levels")]
    [InlineData("/api/admin/logs/stream")]
    [InlineData("/API/ADMIN/LOGS")]
    public void The_log_screen_reading_the_log_is_recognised(string path)
    {
        // The loop this closes: the screen fetches, the fetch is request-logged, the entry rings,
        // the ring pushes, the screen fetches. Left open, an *idle* screen made about five requests
        // a second — worse than the polling the push replaced.
        Assert.True(LogRoutes.IsLogScreenTraffic(path));
    }

    [Theory]
    [InlineData("/api/admin/groups")]
    [InlineData("/api/admin/users")]
    [InlineData("/api/files")]
    [InlineData("/public-api/share/abc")]
    public void Ordinary_traffic_is_not(string path)
    {
        // The guard is a prefix match, so these are what prove it is not too broad — every one of
        // them is an event the live view exists to show.
        Assert.False(LogRoutes.IsLogScreenTraffic(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void An_entry_that_is_not_about_a_request_is_not(string? path)
    {
        // A service's own audit line — "Admin <admin@local> created group" — carries no request
        // path, and is exactly what the screen exists to show.
        Assert.False(LogRoutes.IsLogScreenTraffic(path));
    }

    private static CancellationToken TestTimeout() =>
        new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token;

    /// <summary>
    /// Asserts the signal has nothing queued, by waiting a short while and expecting the wait to be
    /// cancelled rather than to complete.
    /// </summary>
    private static async Task AssertNothingWaiting(LogChangeSignal signal)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await signal.WaitAsync(timeout.Token));
    }
}
