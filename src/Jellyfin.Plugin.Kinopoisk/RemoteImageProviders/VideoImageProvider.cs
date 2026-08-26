using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Kinopoisk.ProviderIdResolvers;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk.MetadataProviders
{
    public class VideoImageProvider : BaseImageProvider
    {
        private readonly ILogger<VideoImageProvider> _logger;
        private readonly IKinopoiskApiClient _apiClient;
        private readonly IProviderIdResolver<BaseItem> _providerIdResolver;

        public override string Name => Constants.ProviderName;

        public VideoImageProvider(IKinopoiskApiClient kinopoiskApiClient, IProviderIdResolver<BaseItem> providerIdResolver, ILogger<VideoImageProvider> logger, IHttpClientFactory httpClientFactory)
            : base(httpClientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiClient = kinopoiskApiClient ?? throw new ArgumentNullException(nameof(kinopoiskApiClient));
            _providerIdResolver = providerIdResolver ?? throw new ArgumentNullException(nameof(providerIdResolver));
        }

        public override bool Supports(BaseItem item)
            => item is Movie || item is Series || item is global::MediaBrowser.Controller.Entities.TV.Season || item is global::MediaBrowser.Controller.Entities.TV.Episode;

        public override IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            if (item is global::MediaBrowser.Controller.Entities.TV.Episode)
                yield return ImageType.Primary;
            else if (item is global::MediaBrowser.Controller.Entities.TV.Season)
                yield return ImageType.Primary;
            else
            {
                yield return ImageType.Primary;
                yield return ImageType.Backdrop;
                yield return ImageType.Logo;
                yield return ImageType.Banner;
                yield return ImageType.Thumb;
            }
        }

        public override async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            if (item is global::MediaBrowser.Controller.Entities.TV.Season season)
                return await GetSeasonImages(season, cancellationToken);

            if (item is global::MediaBrowser.Controller.Entities.TV.Episode episode)
                return await GetEpisodeImages(episode, cancellationToken);

            return await GetVideoImages(item, cancellationToken);
        }

        /// <summary>
        /// Season poster — falls back to the series poster since kinopoisk.dev has no season-specific images.
        /// </summary>
        private async Task<IEnumerable<RemoteImageInfo>> GetSeasonImages(global::MediaBrowser.Controller.Entities.TV.Season season, CancellationToken cancellationToken)
        {
            var seriesKpIdStr = season?.Series?.GetProviderId(Constants.ProviderId);
            if (string.IsNullOrWhiteSpace(seriesKpIdStr) || !int.TryParse(seriesKpIdStr, out var seriesKpId))
                return Enumerable.Empty<RemoteImageInfo>();

            try
            {
                var film = await _apiClient.GetSingleFilm(seriesKpId, cancellationToken);
                var posters = film.ToRemoteImageInfos().Where(i => i.Type == ImageType.Primary);
                return await FilterEmptyImages(posters);
            }
            catch (Exception e)
            {
                _logger.LogDebug(e, "Failed to fetch film poster for season {KinopoiskId}", seriesKpId);
                return Enumerable.Empty<RemoteImageInfo>();
            }
        }

        private async Task<IEnumerable<RemoteImageInfo>> GetVideoImages(BaseItem item, CancellationToken cancellationToken)
        {
            var (resolveResult, kinopoiskId) = await _providerIdResolver.TryResolve(item, cancellationToken);
            if (!resolveResult)
                return Enumerable.Empty<RemoteImageInfo>();

            Film film;
            try
            {
                film = await _apiClient.GetSingleFilm(kinopoiskId, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Failed to fetch film {KinopoiskId} for image providers", kinopoiskId);
                return Enumerable.Empty<RemoteImageInfo>();
            }

            var images = film.ToRemoteImageInfos();

            try
            {
                var stills = await _apiClient.GetImages(kinopoiskId, cancellationToken);
                images = images.Concat(stills.ToRemoteImageInfos());
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Failed to fetch stills for film {KinopoiskId}", kinopoiskId);
            }

            try
            {
                var frames = await _apiClient.GetFrames(kinopoiskId, cancellationToken);
                images = images.Concat(frames.ToRemoteImageInfos());
            }
            catch (Exception e)
            {
                _logger.LogDebug(e, "Failed to fetch frames for film {KinopoiskId}", kinopoiskId);
            }

            return await FilterEmptyImages(images);
        }

        /// <summary>
        /// Episode thumbnail from the series seasons data (kinopoisk.dev).
        /// </summary>
        private async Task<IEnumerable<RemoteImageInfo>> GetEpisodeImages(global::MediaBrowser.Controller.Entities.TV.Episode episode, CancellationToken cancellationToken)
        {
            var seriesKpIdStr = episode?.Series?.GetProviderId(Constants.ProviderId);
            if (string.IsNullOrWhiteSpace(seriesKpIdStr) || !int.TryParse(seriesKpIdStr, out var seriesKpId))
                return Enumerable.Empty<RemoteImageInfo>();

            var seasonNumber = episode.ParentIndexNumber;
            var episodeNumber = episode.IndexNumber;
            if (seasonNumber is null or < 1 || episodeNumber is null or < 1)
                return Enumerable.Empty<RemoteImageInfo>();

            SeasonResponse seasons;
            try
            {
                seasons = await _apiClient.GetSeasons(seriesKpId, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Failed to fetch seasons for series {KinopoiskId}", seriesKpId);
                return Enumerable.Empty<RemoteImageInfo>();
            }

            var apiEpisode = seasons?.Items?
                .FirstOrDefault(s => s?.Number == seasonNumber.Value)?
                .Episodes?
                .FirstOrDefault(e => e?.EpisodeNumber == episodeNumber.Value);

            var stillUrl = apiEpisode?.StillUrl;
            if (string.IsNullOrWhiteSpace(stillUrl))
                return Enumerable.Empty<RemoteImageInfo>();

            return await FilterEmptyImages(new[]
            {
                new RemoteImageInfo
                {
                    Type = ImageType.Primary,
                    Url = stillUrl,
                    Language = Constants.ProviderMetadataLanguage,
                    ProviderName = Constants.ProviderName
                }
            });
        }
    }
}
