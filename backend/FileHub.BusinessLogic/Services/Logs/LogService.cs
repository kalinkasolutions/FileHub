using System.Globalization;
using Dal.Repositories.Logs;
using Dtos.Logs;
using Entities.Logs;
using FileHub.BusinessLogic.Validation;
using Shared;

namespace FileHub.BusinessLogic.Services.Logs;

public sealed class LogService : ILogService
{
    /// <summary>
    /// The hard ceiling on one page, whatever the caller asked for. The DTO's own
    /// <c>[Range(1, 1000)]</c> says the same thing, but this table is unbounded and the cap is the
    /// kind of rule that has to hold even if a route one day binds the query some other way.
    /// </summary>
    private const int MaxTake = 1000;

    private readonly ILogRepository m_logRepository;

    public LogService(ILogRepository logRepository)
    {
        m_logRepository = logRepository;
    }

    public async Task<OperationResult<LogPageDto>> QueryAsync(LogQueryDto dto)
    {
        var validation = DtoValidator.Validate(dto);
        if (validation.HasError)
        {
            return validation.MapError<LogPageDto>();
        }

        // A range the wrong way round matches nothing, which on a log screen looks exactly like a
        // quiet system. Say so instead.
        if (dto.From.HasValue && dto.To.HasValue && dto.From > dto.To)
        {
            return OperationResult<LogPageDto>.Validation(new Dictionary<string, string[]>
            {
                [nameof(dto.From)] = ["The start of the range is after its end."]
            });
        }

        var filter = new LogFilter
        {
            // Null when the name is not a level, which LogLevels.AtOrAbove treats as "every level"
            // rather than "no levels" — see the note there.
            Levels = LogLevels.AtOrAbove(dto.MinLevel),
            Search = dto.Search,
            From = dto.From,
            To = dto.To,
            AfterId = dto.AfterId,
            Take = Math.Clamp(dto.Take, 1, MaxTake)
        };

        var entries = await m_logRepository.QueryAsync(filter);
        var total = await m_logRepository.CountAsync(filter);

        return OperationResult<LogPageDto>.Success(new LogPageDto
        {
            Entries = entries.Select(Map).ToArray(),
            TotalCount = total,
            HasMore = total > entries.Count
        });
    }

    public OperationResult<string[]> ListLevels() =>
        OperationResult<string[]>.Success(LogLevels.All);

    private static LogEntryDto Map(LogEntry entry) => new()
    {
        Id = entry.Id,
        Timestamp = ParseTimestamp(entry.Timestamp),
        Level = entry.Level ?? LogLevels.Information,
        Message = entry.RenderedMessage ?? string.Empty,
        Exception = entry.Exception
    };

    /// <summary>
    /// The sink's text back into a <see cref="DateTime"/> for the wire.
    /// <para>
    /// <see cref="DateTimeStyles.AssumeUniversal"/> is what makes this correct: the stored text
    /// carries no offset, and the sink was configured with <c>storeTimestampInUtc: true</c>, so
    /// without it the value is read as local time and every entry is shifted by the server's
    /// offset. A row that will not parse gets <see cref="DateTime.MinValue"/> rather than taking
    /// the whole page down — one unreadable line is not worth a 500 on a diagnostic screen.
    /// </para>
    /// </summary>
    private static DateTime ParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTime.MinValue;
        }

        var styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, styles, out var parsed))
        {
            return parsed;
        }

        return DateTime.MinValue;
    }
}
