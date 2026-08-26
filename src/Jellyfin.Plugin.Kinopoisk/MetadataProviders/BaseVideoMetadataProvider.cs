using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Kinopoisk.ProviderIdResolvers;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk.MetadataProviders
{
    public abstract class BaseVideoMetadataProvider<TItemType, TLookupInfoType> : BaseMetadataProvider, IRemoteMetadataProvider<TItemType, TLookupInfoType>
        where TItemType : BaseItem, IHasLookupInfo<TLookupInfoType>
        where TLookupInfoType : ItemLookupInfo, new()
    {
        protected readonly ILogger _logger;
        protected readonly IKinopoiskApiClient _apiClient;
        private readonly IProviderIdResolver<TLookupInfoType> _providerIdResolver;

        public BaseVideoMetadataProvider(IKinopoiskApiClient kinopoiskApiClient, IProviderIdResolver<TLookupInfoType> providerIdResolver, ILogger logger, IHttpClientFactory httpClientFactory)
            : base(httpClientFactory)
        {
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
            _apiClient = kinopoiskApiClient ?? throw new System.ArgumentNullException(nameof(kinopoiskApiClient));
            _providerIdResolver = providerIdResolver ?? throw new System.ArgumentNullException(nameof(providerIdResolver));
        }

        protected abstract TItemType ConvertResponseToItem(Film apiResponse);

        /// <summary>
        /// Type filter: prevents assigning movie metadata to a series and vice versa.
        /// </summary>
        protected virtual bool Accepts(Film apiResponse) => true;

        /// <summary>
        /// Additional per-type enrichment (extra api calls).
        /// </summary>
        protected virtual Task PostProcessAsync(TItemType item, Film film, int kinopoiskId, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public async Task<MetadataResult<TItemType>> GetMetadata(TLookupInfoType info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<TItemType>()
            {
                QueriedById = true,
                Provider = Constants.ProviderName,
                ResultLanguage = Constants.ProviderMetadataLanguage
            };

            var (resolveResult, kinopoiskId) = await _providerIdResolver.TryResolve(info, cancellationToken);
            if (!resolveResult)
                return result;

            var film = await _apiClient.GetSingleFilm(kinopoiskId, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (!Accepts(film))
                return result;

            result.Item = ConvertResponseToItem(film);
            if (result.Item != null)
                result.HasMetadata = true;

            var staff = await _apiClient.GetStaff(kinopoiskId, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var sanitizedPersons = await SanitizeEmptyImagePersonInfos(staff.ToPersonInfos());
            foreach (var item in sanitizedPersons)
                result.AddPerson(item);

            var trailers = await _apiClient.GetTrailers(kinopoiskId, cancellationToken);

            var remoteTrailers = trailers.ToMediaUrls();
            if (remoteTrailers is not null)
                result.Item.RemoteTrailers = remoteTrailers;

            await EnrichCardAsync(result.Item, kinopoiskId, cancellationToken);
            await PostProcessAsync(result.Item, film, kinopoiskId, cancellationToken);

            return result;
        }

        private async Task EnrichCardAsync(TItemType item, int kinopoiskId, CancellationToken cancellationToken)
        {
            if (item is null)
                return;

            // Precise premiere date from distributions
            try
            {
                var distributions = await _apiClient.GetDistributions(kinopoiskId, cancellationToken);
                var premiereDate = distributions.SelectPremiereDate();
                if (premiereDate.HasValue)
                    item.PremiereDate = premiereDate;
            }
            catch (Exception e)
            {
                _logger.LogDebug(e, "Distributions fetch failed for {KinopoiskId}", kinopoiskId);
            }

            // Interesting facts appended to the overview
            try
            {
                var facts = await _apiClient.GetFacts(kinopoiskId, cancellationToken);
                var factLines = facts.CollectFactLines();
                if (factLines.Count > 0)
                {
                    var overview = item.Overview?.TrimEnd() ?? string.Empty;
                    item.Overview = overview + (overview.Length > 0 ? "\n\n" : string.Empty) + string.Join("\n", factLines);
                }
            }
            catch (Exception e)
            {
                _logger.LogDebug(e, "Facts fetch failed for {KinopoiskId}", kinopoiskId);
            }
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(TLookupInfoType searchInfo, CancellationToken cancellationToken)
        {
            if (searchInfo.TryGetProviderId(Constants.ProviderId, out var kinopoiskIdStr)
                && int.TryParse(kinopoiskIdStr, out var kinopoiskId))
            {
                var singleResult = (await _apiClient.GetSingleFilm(kinopoiskId, cancellationToken)).ToRemoteSearchResult();
                return Enumerable.Repeat(singleResult, 1);
            }
            else
            {
                return (await _apiClient.SearchByKeyword(searchInfo.Name, cancellationToken: cancellationToken)).ToRemoteSearchResults(_logger);
            }
        }

        protected async Task<IEnumerable<PersonInfo>> SanitizeEmptyImagePersonInfos(IEnumerable<PersonInfo> images)
        {
            using var httpClient = BaseImageProvider.CreateNoRedirectHttpClient();
            var sanitizer = new RemoteImageUrlSanitizer(httpClient);
            var res = await Task.WhenAll(images.Select(async p => {
                p.ImageUrl = await sanitizer.SanitizeRemoteImageUrl(p.ImageUrl);
                return p;
            }));

            return res.Where(i => i != null).ToArray();
        }
    }
}
