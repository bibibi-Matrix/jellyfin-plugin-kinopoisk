using System;
using System.Collections.Generic;
using System.Net.Http;
using Jellyfin.Plugin.Kinopoisk.Configuration;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public static Plugin Instance { get; private set; }

        /// <summary>
        /// Reference to the live switchable client, set by the service registrator.
        /// </summary>
        public SwitchableKinopoiskClient SwitchableClient { get; internal set; }

        /// <summary>
        /// Stored application host for resolving services at runtime.
        /// </summary>
        public IServerApplicationHost ApplicationHost { get; internal set; }

        public override string Name => Constants.PluginName;

        public override string Description => Constants.PluginDescription;

        public override Guid Id => Guid.Parse("33e6d249-648f-44cd-a9ce-497be06c08df");

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = this.Name,
                    EmbeddedResourcePath = string.Format("{0}.Configuration.configPage.html", GetType().Namespace)
                }
            };
        }

        /// <summary>
        /// Called when configuration is updated via the API.
        /// </summary>
        public override void UpdateConfiguration(MediaBrowser.Model.Plugins.BasePluginConfiguration configuration)
        {
            base.UpdateConfiguration(configuration);
            SwapBackendIfNeeded(Configuration);
        }

        /// <summary>
        /// If the backend selection changed, recreate the inner API client and swap it live.
        /// </summary>
        public void SwapBackendIfNeeded(PluginConfiguration configuration)
        {
            if (SwitchableClient == null)
            {
                return;
            }

            try
            {
                var appHost = ApplicationHost;
                if (appHost == null)
                {
                    return;
                }

                var sp = appHost.ServiceProvider;
                if (sp == null)
                {
                    return;
                }

                var newRawClient = KinopoiskPluginServiceRegistrator.CreateRawClient(sp, configuration);
                var newInner = new CachedKinopoiskApiClient(
                    newRawClient,
                    sp.GetRequiredService<IMemoryCache>(),
                    sp.GetRequiredService<ILogger<CachedKinopoiskApiClient>>());

                SwitchableClient.SwitchTo(newInner);
            }
            catch (Exception)
            {
                // Old client continues to work; don't crash the server
            }
        }
    }
}
