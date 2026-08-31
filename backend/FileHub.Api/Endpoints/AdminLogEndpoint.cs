using Dtos.Logs;
using FileHub.BusinessLogic.Services.Logs;
using FileHub.Extensions;
using Shared;

namespace FileHub.Endpoints;

/// <summary>
/// The admin log viewer, over the Serilog sink's table in the same SQLite file.
/// <para>
/// Read-only, and admin-only. The log carries email addresses, base paths, file names and the shape
/// of every account's activity, so it is the most revealing screen in the application — more so
/// than the user list, which at least only names accounts.
/// </para>
/// <para>
/// The live view is pushed, not polled: <see cref="FileHub.Realtime.LogHub"/> (mapped at
/// <c>api/admin/logs/stream</c>) tells the screen that something was written, and the screen
/// answers with a GET here carrying <c>afterId</c>. The hub sends a bare signal and this endpoint
/// still decides what the caller may see, so the filter and the ids have one implementation
/// between them — see the hub for why it is split that way.
/// </para>
/// </summary>
public static class AdminLogEndpoint
{
    public static void MapAdminLogEndpoint(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("api/admin/logs")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        group.MapGet("", QueryAsync);
        group.MapGet("levels", ListLevels);
    }

    /// <summary>
    /// The filter arrives as query parameters rather than a body: this is a GET, and a log view
    /// with a filter in it should survive being bookmarked and reloaded.
    /// </summary>
    private static async Task<IResult> QueryAsync(
        ILogService logService,
        string? minLevel,
        string? search,
        DateTime? from,
        DateTime? to,
        long? afterId,
        int? take)
    {
        var dto = new LogQueryDto
        {
            MinLevel = minLevel,
            Search = search,
            From = from,
            To = to,
            AfterId = afterId,
            // The DTO's own default is the answer when the caller does not ask, so that "how many
            // lines is a page" is written in exactly one place.
            Take = take ?? new LogQueryDto().Take
        };

        return (await logService.QueryAsync(dto)).ToHttpResult();
    }

    private static IResult ListLevels(ILogService logService) =>
        logService.ListLevels().ToHttpResult();
}
