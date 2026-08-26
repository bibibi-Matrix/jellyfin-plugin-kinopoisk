using System.Net.Http;
using Jellyfin.Plugin.Kinopoisk.ProviderIdResolvers;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk
{
    /// <summary>
    /// Registers services
    /// </summary>
    public class KinopoiskPluginServiceRegistrator : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            var configuration = Plugin.Instance?.Configuration ?? new Configuration.PluginConfiguration();

            serviceCollection.AddHttpClient(Constants.NoRedirectHttpClient)
                .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false });

            serviceCollection.AddSingleton((sp) => new KinopoiskApiClient(
                configuration.ApiToken,
                sp.GetRequiredService<ILogger<KinopoiskApiClient>>(),
                sp.GetRequiredService<IHttpClientFactory>(),
                configuration.RequestTimeoutSeconds
            ));

            // Cache first, then rate limiter: cache hits are not throttled.
            serviceCollection.AddSingleton<IKinopoiskApiClient>((sp) => {
                var cached = new CachedKinopoiskApiClient(
                    sp.GetRequiredService<KinopoiskApiClient>(),
                    sp.GetRequiredService<IMemoryCache>(),
                    sp.GetRequiredService<ILogger<CachedKinopoiskApiClient>>()
                );
                return new RateLimitedKinopoiskApiClient(
                    cached,
                    configuration.MaxRequestsPerSecond,
                    sp.GetRequiredService<ILogger<RateLimitedKinopoiskApiClient>>()
                );
            });

            serviceCollection.AddSingleton<IProviderIdResolver<MovieInfo>, VideoResolver<MovieInfo>>();
            serviceCollection.AddSingleton<IProviderIdResolver<SeriesInfo>, VideoResolver<SeriesInfo>>();
            serviceCollection.AddSingleton<IProviderIdResolver<PersonLookupInfo>, CommonResolver<PersonLookupInfo>>();
            serviceCollection.AddSingleton<IProviderIdResolver<BaseItem>, CommonResolver<BaseItem>>();
        }
    }
}
