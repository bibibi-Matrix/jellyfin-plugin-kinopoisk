using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk.MetadataProviders
{
    /// <summary>
    /// Fetches episode names and overviews from Kinopoisk seasons data.
    /// </summary>
    public class EpisodeMetadataProvider : BaseMetadataProvider, IRemoteMetadataProvider<global::MediaBrowser.Controller.Entities.TV.Episode, EpisodeInfo>
    {
        private readonly IKinopoiskApiClient _apiClient;
        private readonly ILogger<EpisodeMetadataProvider> _logger;

        public EpisodeMetadataProvider(IKinopoiskApiClient apiClient, ILogger<EpisodeMetadataProvider> logger, IHttpClientFactory httpClientFactory)
            : base(httpClientFactory)
        {
            _apiClient = apiClient ?? throw new System.ArgumentNullException(nameof(apiClient));
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        }

        public async Task<MetadataResult<global::MediaBrowser.Controller.Entities.TV.Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<global::MediaBrowser.Controller.Entities.TV.Episode>()
            {
                QueriedById = true,
                Provider = Constants.ProviderName,
                ResultLanguage = Constants.ProviderMetadataLanguage
            };

            if (info?.IndexNumber is null)
                return result;

            if (!(Plugin.Instance?.Configuration.EnableEpisodeMetadata ?? true))
                return result;

            var seriesProviderIds = info.SeriesProviderIds;
            if (seriesProviderIds is null
                || !seriesProviderIds.TryGetValue(Constants.ProviderId, out var kinopoiskIdStr)
                || !int.TryParse(kinopoiskIdStr, out var kinopoiskId))
                return result;

            var seasonNumber = info.ParentIndexNumber;
            if (seasonNumber is null or < 1)
                return result;

            SeasonResponse seasons;
            try
            {
                seasons = await _apiClient.GetSeasons(kinopoiskId, cancellationToken);
            }
            catch (System.Exception e)
            {
                _logger.LogWarning(e, "Failed to fetch seasons for series {KinopoiskId}", kinopoiskId);
                return result;
            }

            var apiEpisode = seasons?.Items?
                .FirstOrDefault(s => s?.Number == seasonNumber.Value)?
                .Episodes?
                .FirstOrDefault(e => e?.EpisodeNumber == info.IndexNumber.Value);

            if (apiEpisode is null)
                return result;

            var name = ApiModelExtensions.GetLocalName(apiEpisode);
            if (string.IsNullOrWhiteSpace(name))
                return result;

            result.Item = new global::MediaBrowser.Controller.Entities.TV.Episode
            {
                Name = name,
                Overview = ApiModelExtensions.GetOverview(apiEpisode),
                PremiereDate = apiEpisode.ReleaseDate.ParseDate()
            };
            result.HasMetadata = true;

            return result;
        }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
            => Task.FromResult(Enumerable.Empty<RemoteSearchResult>()); // Not supported
    }
}
