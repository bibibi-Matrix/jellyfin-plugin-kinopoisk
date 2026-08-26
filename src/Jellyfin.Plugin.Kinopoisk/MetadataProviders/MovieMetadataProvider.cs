using System.Net.Http;
using Jellyfin.Plugin.Kinopoisk.ProviderIdResolvers;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk.MetadataProviders
{
    public class MovieMetadataProvider : BaseVideoMetadataProvider<Movie, MovieInfo>
    {
        public MovieMetadataProvider(IKinopoiskApiClient kinopoiskApiClient, IProviderIdResolver<MovieInfo> providerIdResolver, ILogger<MovieMetadataProvider> logger, IHttpClientFactory httpClientFactory)
            : base(kinopoiskApiClient, providerIdResolver, logger, httpClientFactory)
        {
        }

        protected override Movie ConvertResponseToItem(Film apiResponse)
            => apiResponse.ToMovie();

        protected override bool Accepts(Film apiResponse)
            => apiResponse.IsFilmLike();
    }

    internal static class FilmTypeExtensions
    {
        public static bool IsFilmLike(this Film film)
            => film is null
                || film.Type == KinopoiskUnofficialInfo.ApiClient.FilmType.FILM
                || film.Type == KinopoiskUnofficialInfo.ApiClient.FilmType.VIDEO
                || film.ShortFilm;

        public static bool IsSeriesLike(this Film film)
            => film is null
                || !film.IsFilmLike();
    }
}
