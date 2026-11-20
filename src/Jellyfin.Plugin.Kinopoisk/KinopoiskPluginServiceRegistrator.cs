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

            serviceCollection.AddSingleton<SwitchableKinopoiskClient>((sp) =>
            {
                // Read the configuration when the client is actually resolved instead of
                // capturing it here: Plugin.Instance is still null while RegisterServices
                // runs, so a captured value would always be the default configuration.
                var configuration = Plugin.Instance?.Configuration ?? new Configuration.PluginConfiguration();
                var sw = new SwitchableKinopoiskClient(
                    CreateCachedClient(sp, configuration),
                    sp.GetRequiredService<ILogger<SwitchableKinopoiskClient>>());
                SwitchableClient = sw;
                return sw;
            });
            serviceCollection.AddSingleton<IKinopoiskApiClient>(sp => sp.GetRequiredService<SwitchableKinopoiskClient>());

            serviceCollection.AddHostedService<KinopoiskBackendInitializer>();

            serviceCollection.AddSingleton<IProviderIdResolver<MovieInfo>, VideoResolver<MovieInfo>>();
            serviceCollection.AddSingleton<IProviderIdResolver<SeriesInfo>, VideoResolver<SeriesInfo>>();
            serviceCollection.AddSingleton<IProviderIdResolver<PersonLookupInfo>, CommonResolver<PersonLookupInfo>>();
            serviceCollection.AddSingleton<IProviderIdResolver<BaseItem>, CommonResolver<BaseItem>>();
        }

        internal static CachedKinopoiskApiClient CreateCachedClient(IServiceProvider sp, Configuration.PluginConfiguration configuration)
        {
            return new CachedKinopoiskApiClient(
                CreateRawClient(sp, configuration),
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<ILogger<CachedKinopoiskApiClient>>());
        }

        /// <summary>
        /// Rebuilds the API client from the given configuration and swaps it into the
        /// running <see cref="SwitchableKinopoiskClient"/>. Returns false when the
        /// client has not been created yet (the factory will pick up the saved
        /// configuration on first resolution).
        /// </summary>
        internal static bool ApplyConfiguration(IServiceProvider sp, Configuration.PluginConfiguration configuration)
        {
            var sw = SwitchableClient;
            if (sw == null || sp == null || configuration == null)
            {
                return false;
            }

            sw.SwitchTo(CreateCachedClient(sp, configuration));
            return true;
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
