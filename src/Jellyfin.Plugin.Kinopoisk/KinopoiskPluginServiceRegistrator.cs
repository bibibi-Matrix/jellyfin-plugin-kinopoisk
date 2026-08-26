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

            // Store application host so Plugin can recreate client on config change
            Plugin.Instance.ApplicationHost = applicationHost;

            serviceCollection.AddSingleton<SwitchableKinopoiskClient>((sp) =>
            {
                var inner = new CachedKinopoiskApiClient(
                    CreateRawClient(sp, configuration),
                    sp.GetRequiredService<IMemoryCache>(),
                    sp.GetRequiredService<ILogger<CachedKinopoiskApiClient>>());
                var sw = new SwitchableKinopoiskClient(inner, sp.GetRequiredService<ILogger<SwitchableKinopoiskClient>>());
                Plugin.Instance.SwitchableClient = sw;
                return sw;
            });
            serviceCollection.AddSingleton<IKinopoiskApiClient>(sp => sp.GetRequiredService<SwitchableKinopoiskClient>());

            serviceCollection.AddSingleton<IProviderIdResolver<MovieInfo>, VideoResolver<MovieInfo>>();
            serviceCollection.AddSingleton<IProviderIdResolver<SeriesInfo>, VideoResolver<SeriesInfo>>();
            serviceCollection.AddSingleton<IProviderIdResolver<PersonLookupInfo>, CommonResolver<PersonLookupInfo>>();
            serviceCollection.AddSingleton<IProviderIdResolver<BaseItem>, CommonResolver<BaseItem>>();
        }

        /// <summary>
        /// Creates the appropriate raw API client (not wrapped in cache/switchable).
        /// Public so Plugin can call it on config change.
        /// </summary>
        public static IKinopoiskApiClient CreateRawClient(IServiceProvider sp, Configuration.PluginConfiguration configuration)
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
