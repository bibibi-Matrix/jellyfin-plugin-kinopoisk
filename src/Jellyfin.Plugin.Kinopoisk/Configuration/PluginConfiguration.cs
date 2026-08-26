using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Kinopoisk.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        // https://kinopoiskapiunofficial.tech/
        public string ApiToken { get; set; } = "85d30ae5-d875-4c5f-900d-8e37bb20625e";

        /// <summary>
        /// Gets or sets the maximum API requests per second.
        /// </summary>
        public int MaxRequestsPerSecond { get; set; } = 8;

        /// <summary>
        /// Gets or sets the request timeout in seconds.
        /// </summary>
        public int RequestTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets a value indicating whether to download backdrops/stills.
        /// </summary>
        public bool EnableBackdrops { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to fetch episode metadata for series.
        /// </summary>
        public bool EnableEpisodeMetadata { get; set; } = true;
    }
}
