using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Kinopoisk.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Backend algorithm: KinopoiskApiUnofficial (kinopoiskapiunofficial.tech) or KinopoiskDev (kinopoisk.dev).
        /// </summary>
        public string Backend { get; set; } = "KinopoiskApiUnofficial";

        // https://kinopoiskapiunofficial.tech/
        public string ApiToken { get; set; } = "85d30ae5-d875-4c5f-900d-8e37bb20625e";

        // https://kinopoisk.dev - get your own free token
        public string ApiDevToken { get; set; } = string.Empty;
    }
}
