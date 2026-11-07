using Entities.Paths;
using FileHub.BusinessLogic.Media;
using FileHub.BusinessLogic.Services.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FileHub.IntegrationTests;

public static class FakeMediaLookupServices
{
    /// <summary>
    /// <see cref="MediaInfoService"/> over fake sources, for fixtures that create links — creating
    /// one on a marked disk warms its poster, and resolving one may queue a refresh. A fixture that registered its own fakes keeps them.
    /// </summary>
    public static void AddFakeMediaLookupIfMissing(this IServiceCollection services)
    {
        if (services.All(d => d.ServiceType != typeof(IMediaLookupProvider)))
        {
            services.AddSingleton<IMediaLookupProvider>(new FakeLookupProvider(BasePathKind.Media));
            services.AddSingleton<IMediaLookupProvider>(new FakeLookupProvider(BasePathKind.Games));
        }

        services.TryAddSingleton<IPosterFetcher, FakePosterFetcher>();
        services.TryAddScoped<IMediaInfoService, MediaInfoService>();
        services.TryAddSingleton<MediaRefreshQueue>();
        services.TryAddSingleton<MediaRefreshWorker>();
    }
}
