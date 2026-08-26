using System;
using System.Net.Http;
using Jellyfin.Plugin.Kinopoisk.ProviderIdResolvers;
using KinopoiskDevAdapter;
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

            serviceCollection.AddSingleton<IKinopoiskApiClient>((sp) => new CachedKinopoiskApiClient(
                CreateRawClient(sp, configuration),
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<ILogger<CachedKinopoiskApiClient>>()
            ));

            serviceCollection.AddSingleton<IProviderIdResolver<MovieInfo>, VideoResolver<MovieInfo>>();
            serviceCollection.AddSingleton<IProviderIdResolver<SeriesInfo>, VideoResolver<SeriesInfo>>();
            serviceCollection.AddSingleton<IProviderIdResolver<PersonLookupInfo>, CommonResolver<PersonLookupInfo>>();
            serviceCollection.AddSingleton<IProviderIdResolver<BaseItem>, CommonResolver<BaseItem>>();
        }

        private static IKinopoiskApiClient CreateRawClient(IServiceProvider sp, Configuration.PluginConfiguration configuration)
        {
            if (string.Equals(configuration.Backend, "KinopoiskDev", StringComparison.OrdinalIgnoreCase))
            {
                return new KinopoiskDevClient(
                    configuration.ApiDevToken,
                    sp.GetRequiredService<ILogger<KinopoiskDevClient>>(),
                    sp.GetRequiredService<IHttpClientFactory>());
            }

            // Default algorithm: kinopoiskapiunofficial.tech
            return new KinopoiskApiClient(
                configuration.ApiToken,
                sp.GetRequiredService<ILogger<KinopoiskApiClient>>(),
                sp.GetRequiredService<IHttpClientFactory>());
        }
    }
}
