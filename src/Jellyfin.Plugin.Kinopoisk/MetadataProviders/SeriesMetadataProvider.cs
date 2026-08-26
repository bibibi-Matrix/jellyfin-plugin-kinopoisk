using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Kinopoisk.ProviderIdResolvers;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk.MetadataProviders
{
    public class SeriesMetadataProvider : BaseVideoMetadataProvider<Series, SeriesInfo>
    {
        public SeriesMetadataProvider(IKinopoiskApiClient kinopoiskApiClient, IProviderIdResolver<SeriesInfo> providerIdResolver, ILogger<SeriesMetadataProvider> logger, IHttpClientFactory httpClientFactory)
            : base(kinopoiskApiClient, providerIdResolver, logger, httpClientFactory)
        {
        }

        protected override Series ConvertResponseToItem(Film apiResponse)
            => apiResponse.ToSeries();

        protected override bool Accepts(Film apiResponse)
            => apiResponse.IsSeriesLike();

        /// <summary>
        /// Derives series status and dates from episode air dates.
        /// </summary>
        protected override async Task PostProcessAsync(Series item, int kinopoiskId, CancellationToken cancellationToken)
        {
            if (item is null)
                return;

            try
            {
                var seasons = await _apiClient.GetSeasons(kinopoiskId, cancellationToken);
                var episodeDates = seasons?.Items?
                    .SelectMany(s => s?.Episodes ?? Array.Empty<global::KinopoiskUnofficialInfo.ApiClient.Episode>())
                    .Select(e => e?.ReleaseDate.ParseDate())
                    .Where(d => d.HasValue)
                    .Select(d => d.Value)
                    .ToList();

                if (episodeDates is null || episodeDates.Count < 1)
                    return;

                var first = episodeDates.Min();
                var last = episodeDates.Max();

                if (!item.PremiereDate.HasValue || item.PremiereDate > first)
                    item.PremiereDate = first;

                item.EndDate = last;
                item.Status = last > DateTime.UtcNow.AddDays(-45)
                    ? SeriesStatus.Continuing
                    : SeriesStatus.Ended;
            }
            catch (Exception e)
            {
                _logger.LogDebug(e, "Seasons fetch failed for {KinopoiskId}", kinopoiskId);
            }
        }
    }
}
