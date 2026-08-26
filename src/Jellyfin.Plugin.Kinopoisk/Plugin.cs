using System;
using System.Collections.Generic;
using System.Net.Http;
using Jellyfin.Plugin.Kinopoisk.Configuration;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
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

        public override void UpdateConfiguration(MediaBrowser.Model.Plugins.BasePluginConfiguration configuration)
        {
            base.UpdateConfiguration(configuration);
            SwapBackendIfNeeded(Configuration);
        }

        private void SwapBackendIfNeeded(PluginConfiguration configuration)
        {
            var sw = KinopoiskPluginServiceRegistrator.SwitchableClient;
            var appHost = KinopoiskPluginServiceRegistrator.ApplicationHost;
            if (sw == null || appHost?.ServiceProvider == null)
            {
                return;
            }

            try
            {
                var sp = appHost.ServiceProvider;
                var newRawClient = KinopoiskPluginServiceRegistrator.CreateRawClient(sp, configuration);
                var newInner = new CachedKinopoiskApiClient(
                    newRawClient,
                    sp.GetRequiredService<IMemoryCache>(),
                    sp.GetRequiredService<ILogger<CachedKinopoiskApiClient>>());

                sw.SwitchTo(newInner);
            }
            catch
            {
                // Old client continues to work
            }
        }
    }
}
