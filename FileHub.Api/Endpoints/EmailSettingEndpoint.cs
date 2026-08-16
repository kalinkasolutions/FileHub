using Dtos.Email;
using FileHub.BusinessLogic.Services.Email;
using FileHub.Extensions;
using Shared;

namespace FileHub.Endpoints;

public static class EmailSettingEndpoint
{
    public static void MapEmailSettingEndpoint(this IEndpointRouteBuilder builder)
    {
        // Admin-only: these settings hold an SMTP credential and decide where account mail goes.
        var group = builder.MapGroup("api/admin/email")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        group.MapGet("settings", GetAsync);
        group.MapPut("settings", UpdateAsync);

        // Sends a real message, so it is a POST rather than a read of the settings.
        group.MapPost("test", SendTestAsync);
    }

    private static async Task<IResult> GetAsync(IEmailSettingService service)
    {
        return (await service.GetAsync()).ToHttpResult();
    }

    private static async Task<IResult> UpdateAsync(UpdateEmailSettingDto dto, IEmailSettingService service)
    {
        return (await service.UpdateAsync(dto)).ToHttpResult();
    }

    private static async Task<IResult> SendTestAsync(SendTestEmailDto dto, IEmailSettingService service)
    {
        return (await service.SendTestAsync(dto)).ToHttpResult();
    }
}
