using System;
using System.Text.RegularExpressions;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk
{
    public static class Constants
    {
        public const string PluginName = "КиноПоиск";
        public const string PluginDescription = "Информация о фильмах и сериалах с КиноПоиска";
        public const string ProviderId = "kinopoisk";
        public const string ProviderName = "КиноПоиск";
        public const string ProviderMetadataLanguage = "ru";

        /// <summary>
        /// Name of the http client that does not follow redirects (used for image url validation).
        /// </summary>
        public const string NoRedirectHttpClient = "KinopoiskNoRedirect";
    }
}
