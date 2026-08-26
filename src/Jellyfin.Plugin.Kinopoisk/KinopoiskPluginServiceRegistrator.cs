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
    public class KinopoiskPluginServiceRegistrator : IPluginServiceRegistrator
    {
        /// <summary>
        /// Static references for Plugin to access after registration.
        /// Plugin.Instance is not yet set when RegisterServices is called.
        /// </summary>
        internal static SwitchableKinopoiskClient SwitchableClient { get; private set; }
        internal static IServerApplicationHost ApplicationHost { get; private set; }

        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            ApplicationHost = applicationHost;
            var configuration = Plugin.Instance?.Configuration ?? new Configuration.PluginConfiguration();

            serviceCollection.AddSingleton<SwitchableKinopoiskClient>((sp) =>
            {
                var inner = new CachedKinopoiskApiClient(
                    CreateRawClient(sp, configuration),
                    sp.GetRequiredService<IMemoryCache>(),
                    sp.GetRequiredService<ILogger<CachedKinopoiskApiClient>>());
                var sw = new SwitchableKinopoiskClient(inner, sp.GetRequiredService<ILogger<SwitchableKinopoiskClient>>());
                SwitchableClient = sw;
                return sw;
            });
            serviceCollection.AddSingleton<IKinopoiskApiClient>(sp => sp.GetRequiredService<SwitchableKinopoiskClient>());

            serviceCollection.AddSingleton<IProviderIdResolver<MovieInfo>, VideoResolver<MovieInfo>>();
            serviceCollection.AddSingleton<IProviderIdResolver<SeriesInfo>, VideoResolver<SeriesInfo>>();
            serviceCollection.AddSingleton<IProviderIdResolver<PersonLookupInfo>, CommonResolver<PersonLookupInfo>>();
            serviceCollection.AddSingleton<IProviderIdResolver<BaseItem>, CommonResolver<BaseItem>>();
        }

        public static IKinopoiskApiClient CreateRawClient(IServiceProvider sp, Configuration.PluginConfiguration configuration)
        {
            if (string.Equals(configuration.Backend, "KinopoiskDev", StringComparison.OrdinalIgnoreCase))
            {
                return new KinopoiskDevClient(
                    configuration.ApiDevToken,
                    sp.GetRequiredService<ILogger<KinopoiskDevClient>>(),
                    sp.GetRequiredService<IHttpClientFactory>());
            }

            return new KinopoiskApiClient(
                configuration.ApiToken,
                sp.GetRequiredService<ILogger<KinopoiskApiClient>>(),
                sp.GetRequiredService<IHttpClientFactory>());
        }
    }
}
