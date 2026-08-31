using Dtos.Logs;
using Shared;

namespace FileHub.BusinessLogic.Services.Logs;

/// <summary>
/// The admin log viewer. Read-only: this reads the Serilog sink's table so an operator can see what
/// the install has been doing without shelling into the container for <c>docker compose logs</c>.
/// </summary>
public interface ILogService
{
    Task<OperationResult<LogPageDto>> QueryAsync(LogQueryDto dto);

    /// <summary>The level names the screen's filter offers, least severe first.</summary>
    OperationResult<string[]> ListLevels();
}
