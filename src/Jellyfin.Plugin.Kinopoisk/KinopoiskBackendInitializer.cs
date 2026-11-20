using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk
{
    /// <summary>
    /// Applies the saved backend configuration once the plugin instance is available.
    /// <para>
    /// Jellyfin calls <see cref="KinopoiskPluginServiceRegistrator.RegisterServices"/> before
    /// <see cref="Plugin.Instance"/> is set, so the API client cannot see the saved settings
    /// at that point and would fall back to the default backend (KinopoiskApiUnofficial).
    /// This hosted service runs after startup and swaps the client to the configured backend
    /// (for example KinopoiskDev) without requiring the settings to be saved again.
    /// </para>
    /// </summary>
    public sealed class KinopoiskBackendInitializer : IHostedService
    {
        private readonly IServerApplicationHost _applicationHost;
        private readonly ILogger<KinopoiskBackendInitializer> _logger;

        public KinopoiskBackendInitializer(
            IServerApplicationHost applicationHost,
            ILogger<KinopoiskBackendInitializer> logger)
        {
            _applicationHost = applicationHost;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Plugin instances are normally created right after service registration;
            // wait a moment in case startup order differs between Jellyfin versions.
            for (var i = 0; i < 100 && Plugin.Instance == null && !cancellationToken.IsCancellationRequested; i++)
            {
                await Task.Delay(100).ConfigureAwait(false);
            }

            var configuration = Plugin.Instance?.Configuration;
            if (configuration == null)
            {
                _logger.LogWarning("[Kinopoisk] Plugin instance is not available yet; the backend will be read from the configuration on first use");
                return;
            }

            try
            {
                var serviceProvider = _applicationHost?.ServiceProvider;
                if (serviceProvider == null)
                {
                    _logger.LogWarning("[Kinopoisk] Service provider is not available; skipping backend initialization");
                    return;
                }

                // Resolve the switchable client first so it exists even if nothing has
                // requested it yet, then force the saved configuration onto it.
                var switchable = serviceProvider.GetService<SwitchableKinopoiskClient>();
                if (switchable == null)
                {
                    _logger.LogWarning("[Kinopoisk] Switchable client is not registered; skipping backend initialization");
                    return;
                }

                if (KinopoiskPluginServiceRegistrator.ApplyConfiguration(serviceProvider, configuration))
                {
                    _logger.LogInformation("[Kinopoisk] Backend applied from saved configuration: {Backend}", configuration.Backend);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Kinopoisk] Failed to apply the saved backend configuration");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
