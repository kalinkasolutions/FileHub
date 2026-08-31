using Dtos.Logs;
using Shared;

namespace FileHub.IntegrationTests;

/// <summary>
/// The admin log viewer's filters. Every one of these is a place where the query can be quietly
/// wrong rather than loudly broken — a level filter that hides errors, a date range that compares
/// two different text formats, a search term whose wildcards match everything — so each is pinned
/// against rows written the way the Serilog sink writes them.
/// </summary>
public sealed class LogQueryTests : LogTestBase
{
    private static readonly DateTime Noon = new(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task The_newest_entries_come_back_first()
    {
        WriteLog(Noon, LogLevels.Information, "first");
        WriteLog(Noon.AddMinutes(1), LogLevels.Information, "second");
        WriteLog(Noon.AddMinutes(2), LogLevels.Information, "third");

        var result = await Logs.QueryAsync(new LogQueryDto());

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(["third", "second", "first"], result.Value.Entries.Select(x => x.Message));
    }

    [Fact]
    public async Task A_minimum_level_includes_everything_above_it()
    {
        WriteLog(Noon, LogLevels.Debug, "debug");
        WriteLog(Noon, LogLevels.Information, "information");
        WriteLog(Noon, LogLevels.Warning, "warning");
        WriteLog(Noon, LogLevels.Error, "error");
        WriteLog(Noon, LogLevels.Fatal, "fatal");

        var result = await Logs.QueryAsync(new LogQueryDto { MinLevel = LogLevels.Warning });

        // The whole point of a *minimum*: asking for warnings must not hide the error and the fatal
        // that followed. A plain `Level >= 'Warning'` in SQL would have done exactly that.
        Assert.Equal(["fatal", "error", "warning"], result.Value.Entries.Select(x => x.Message));
    }

    [Fact]
    public async Task An_unknown_level_name_does_not_filter_anything_out()
    {
        WriteLog(Noon, LogLevels.Information, "information");
        WriteLog(Noon, LogLevels.Error, "error");

        var result = await Logs.QueryAsync(new LogQueryDto { MinLevel = "Critical" });

        // "Critical" is the Microsoft spelling, not Serilog's. It must read as "no level filter"
        // rather than "no levels at all" — an empty log screen looks like a quiet system.
        Assert.Equal(2, result.Value.Entries.Length);
    }

    [Fact]
    public async Task The_message_search_ignores_case()
    {
        WriteLog(Noon, LogLevels.Information, "Admin deleted group \"Family\"");

        var result = await Logs.QueryAsync(new LogQueryDto { Search = "deleted group" });

        Assert.Single(result.Value.Entries);
    }

    [Fact]
    public async Task A_percent_in_the_search_term_is_a_literal()
    {
        WriteLog(Noon, LogLevels.Information, "disk is 90% full");
        WriteLog(Noon, LogLevels.Information, "nothing to do with disks");

        var result = await Logs.QueryAsync(new LogQueryDto { Search = "90%" });

        // Unescaped, "%" is LIKE's own wildcard and this term would match every row in the table.
        Assert.Single(result.Value.Entries);
        Assert.Equal("disk is 90% full", result.Value.Entries[0].Message);
    }

    [Fact]
    public async Task An_underscore_in_the_search_term_is_a_literal()
    {
        WriteLog(Noon, LogLevels.Information, "read base_path");
        WriteLog(Noon, LogLevels.Information, "read basexpath");

        var result = await Logs.QueryAsync(new LogQueryDto { Search = "base_path" });

        // "_" is LIKE's single-character wildcard, so unescaped this matches "basexpath" too.
        Assert.Single(result.Value.Entries);
        Assert.Equal("read base_path", result.Value.Entries[0].Message);
    }

    [Fact]
    public async Task The_date_range_is_inclusive_at_both_ends()
    {
        WriteLog(Noon.AddHours(-1), LogLevels.Information, "before");
        WriteLog(Noon, LogLevels.Information, "at the start");
        WriteLog(Noon.AddHours(1), LogLevels.Information, "at the end");
        WriteLog(Noon.AddHours(2), LogLevels.Information, "after");

        var result = await Logs.QueryAsync(new LogQueryDto { From = Noon, To = Noon.AddHours(1) });

        Assert.Equal(["at the end", "at the start"], result.Value.Entries.Select(x => x.Message));
    }

    [Fact]
    public async Task A_local_time_bound_is_compared_in_utc()
    {
        WriteLog(Noon, LogLevels.Information, "at noon utc");

        // The stored text is UTC with no offset. A bound arriving as a local DateTime has to be
        // converted before it is formatted, or the range is off by the server's offset — which on a
        // machine east of UTC silently excludes the row it was meant to include.
        var localNoon = Noon.ToLocalTime();
        var result = await Logs.QueryAsync(new LogQueryDto
        {
            From = localNoon.AddMinutes(-1),
            To = localNoon.AddMinutes(1)
        });

        Assert.Single(result.Value.Entries);
    }

    [Fact]
    public async Task A_range_the_wrong_way_round_is_a_validation_error()
    {
        WriteLog(Noon, LogLevels.Information, "something");

        var result = await Logs.QueryAsync(new LogQueryDto { From = Noon.AddHours(1), To = Noon });

        // Rather than an empty page, which on a log screen is indistinguishable from a quiet system.
        Assert.Equal(ResultCode.Validation, result.ResultCode);
    }

    [Fact]
    public async Task AfterId_returns_only_what_has_arrived_since()
    {
        WriteLog(Noon, LogLevels.Information, "first");
        WriteLog(Noon.AddMinutes(1), LogLevels.Information, "second");

        var firstPage = await Logs.QueryAsync(new LogQueryDto());
        var newest = firstPage.Value.Entries.Max(x => x.Id);

        WriteLog(Noon.AddMinutes(2), LogLevels.Information, "third");

        var tail = await Logs.QueryAsync(new LogQueryDto { AfterId = newest });

        Assert.Equal(["third"], tail.Value.Entries.Select(x => x.Message));
    }

    [Fact]
    public async Task AfterId_narrows_the_page_but_not_the_count()
    {
        WriteLog(Noon, LogLevels.Information, "first");
        WriteLog(Noon.AddMinutes(1), LogLevels.Information, "second");

        var firstPage = await Logs.QueryAsync(new LogQueryDto());
        var newest = firstPage.Value.Entries.Max(x => x.Id);

        WriteLog(Noon.AddMinutes(2), LogLevels.Information, "third");

        var tail = await Logs.QueryAsync(new LogQueryDto { AfterId = newest });

        // "1 new line" and "3 lines match the filter" are two different questions, and the screen
        // asks both at once: the tally must not collapse to the size of the tail.
        Assert.Single(tail.Value.Entries);
        Assert.Equal(3, tail.Value.TotalCount);
    }

    [Fact]
    public async Task Take_caps_the_page_and_HasMore_says_it_was_cut_off()
    {
        for (var i = 0; i < 5; i++)
        {
            WriteLog(Noon.AddMinutes(i), LogLevels.Information, $"line {i}");
        }

        var result = await Logs.QueryAsync(new LogQueryDto { Take = 2 });

        Assert.Equal(2, result.Value.Entries.Length);
        Assert.Equal(5, result.Value.TotalCount);
        Assert.True(result.Value.HasMore);
    }

    [Fact]
    public async Task A_timestamp_with_no_offset_is_read_as_utc()
    {
        WriteLog(Noon, LogLevels.Information, "at noon utc");

        var result = await Logs.QueryAsync(new LogQueryDto());

        // The sink stores UTC without a suffix. Parsed without AssumeUniversal the value is taken as
        // local time and every entry on the screen is shifted by the server's offset.
        Assert.Equal(Noon, result.Value.Entries[0].Timestamp);
    }

    [Fact]
    public async Task An_exception_comes_back_with_its_entry()
    {
        WriteLog(Noon, LogLevels.Error, "it broke", "System.InvalidOperationException: nope");

        var result = await Logs.QueryAsync(new LogQueryDto());

        Assert.Equal("System.InvalidOperationException: nope", result.Value.Entries[0].Exception);
    }

    [Fact]
    public async Task Filters_combine()
    {
        WriteLog(Noon, LogLevels.Information, "Admin created group \"Family\"");
        WriteLog(Noon, LogLevels.Warning, "Admin created group \"Work\"");
        WriteLog(Noon.AddDays(-2), LogLevels.Warning, "Admin created group \"Old\"");
        WriteLog(Noon, LogLevels.Warning, "Admin deleted base path");

        var result = await Logs.QueryAsync(new LogQueryDto
        {
            MinLevel = LogLevels.Warning,
            Search = "created group",
            From = Noon.AddHours(-1),
            To = Noon.AddHours(1)
        });

        Assert.Single(result.Value.Entries);
        Assert.Equal("Admin created group \"Work\"", result.Value.Entries[0].Message);
    }

    [Fact]
    public async Task The_level_list_is_ordered_least_severe_first()
    {
        var result = Logs.ListLevels();

        Assert.Equal(
            [LogLevels.Verbose, LogLevels.Debug, LogLevels.Information, LogLevels.Warning, LogLevels.Error, LogLevels.Fatal],
            result.Value);
    }
}
