using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk.MetadataProviders
{
    public class SeasonMetadataProvider : BaseMetadataProvider, IRemoteMetadataProvider<global::MediaBrowser.Controller.Entities.TV.Season, SeasonInfo>
    {
        private readonly IKinopoiskApiClient _apiClient;
        private readonly ILogger<SeasonMetadataProvider> _logger;

        public SeasonMetadataProvider(IKinopoiskApiClient apiClient, ILogger<SeasonMetadataProvider> logger, IHttpClientFactory httpClientFactory)
            : base(httpClientFactory)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<MetadataResult<global::MediaBrowser.Controller.Entities.TV.Season>> GetMetadata(SeasonInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<global::MediaBrowser.Controller.Entities.TV.Season>
            {
                QueriedById = true,
                Provider = Constants.ProviderName,
                ResultLanguage = Constants.ProviderMetadataLanguage
            };

            if (info?.IndexNumber is null or < 1)
                return result;

            var seriesProviderIds = info.SeriesProviderIds;
            if (seriesProviderIds is null
                || !seriesProviderIds.TryGetValue(Constants.ProviderId, out var kinopoiskIdStr)
                || !int.TryParse(kinopoiskIdStr, out var kinopoiskId))
                return result;

            var seasonNumber = info.IndexNumber.Value;

            try
            {
                var seasons = await _apiClient.GetSeasons(kinopoiskId, cancellationToken);
                var apiSeason = seasons?.Items?
                    .FirstOrDefault(s => s?.Number == seasonNumber);

                if (apiSeason is null)
                    return result;

                result.Item = new global::MediaBrowser.Controller.Entities.TV.Season
                {
                    Name = ApiModelExtensions.GetLocalSeasonName(apiSeason, seasonNumber),
                    IndexNumber = seasonNumber,
                    PremiereDate = apiSeason.AirDate.ParseDate()
                };
                result.HasMetadata = true;
            }
            catch (Exception e)
            {
                _logger.LogDebug(e, "Seasons fetch failed for {KinopoiskId} season {Season}", kinopoiskId, seasonNumber);
            }

            return result;
        }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeasonInfo searchInfo, CancellationToken cancellationToken)
            => Task.FromResult(Enumerable.Empty<RemoteSearchResult>());
    }
}
