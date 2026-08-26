using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk.ProviderIdResolvers
{
    public class VideoResolver<T> : CommonLookupInfoResolver<T>
        where T : ItemLookupInfo
    {
        private readonly IKinopoiskApiClient _kinopoiskApiClient;

        public VideoResolver(IKinopoiskApiClient kinopoiskApiClient, ILogger<VideoResolver<T>> logger) : base(logger)
        {
            _kinopoiskApiClient = kinopoiskApiClient ?? throw new ArgumentNullException(nameof(kinopoiskApiClient));
        }

        public override async Task<(bool IsSuccess, int ProviderId)> TryResolve(T info, CancellationToken? ct = null)
        {
            var possibleResult = await base.TryResolve(info, ct);
            if (possibleResult.IsSuccess)
                return possibleResult;

            if (string.IsNullOrWhiteSpace(info.Name))
            {
                _logger.LogDebug("Film name is empty, skipping KinopoiskProviderId search");
                return (false, 0);
            }

            _logger.LogDebug("Trying to get suitable film with name '{Name}'...", info.Name);
            var searchResult = await _kinopoiskApiClient.SearchByKeyword(info.Name, 1, ct ?? CancellationToken.None);
            if (searchResult.SearchFilmsCountResult < 1 || searchResult?.Films.Count < 1)
            {
                _logger.LogDebug("Received empty search result");
                return (false, 0);
            }
            var candidates = searchResult.Films.ToArray();
            _logger.LogDebug("Received {Count} results, trying to filter and match...", candidates.Length);

            var candidates_by_year = FilterByYear(info, candidates);
            var candidates_by_type = FilterByType(candidates);

            FilmSearchResponse_films[] candidates_year_and_type;
            if (candidates_by_year.Count > 0 && candidates_by_type.Count > 0)
                candidates_year_and_type = candidates_by_year.Where(f => candidates_by_type.Contains(f)).ToArray();
            else
                candidates_year_and_type = Array.Empty<FilmSearchResponse_films>();

            possibleResult = await TryResolveBySingleCandidateLeft(info, candidates_year_and_type, ct);
            if (possibleResult.IsSuccess)
                return possibleResult;

            possibleResult = await TryResolveBySingleCandidateLeft(info, candidates_by_year, ct);
            if (possibleResult.IsSuccess)
                return possibleResult;

            possibleResult = await TryResolveBySingleCandidateLeft(info, candidates_by_type, ct);
            if (possibleResult.IsSuccess)
                return possibleResult;

            possibleResult = await TryResolveByImdbMatch(info, candidates_year_and_type, ct);
            if (possibleResult.IsSuccess)
                return possibleResult;

            possibleResult = await TryResolveByImdbMatch(info, candidates_by_year, ct);
            if (possibleResult.IsSuccess)
                return possibleResult;

            possibleResult = await TryResolveByImdbMatch(info, candidates, ct);
            if (possibleResult.IsSuccess)
                return possibleResult;

            if (0 < candidates_year_and_type.Length)
            {
                var kinopoiskId = candidates_year_and_type.First().FilmId;
                _logger.LogDebug("All other checks failed, use first result by year+type, setting KinopoiskProviderId to {Id} ({Name})", kinopoiskId, info.Name);
                return (true, kinopoiskId);
            }

            if (0 < candidates_by_year.Count)
            {
                var kinopoiskId = candidates_by_year.First().FilmId;
                _logger.LogDebug("All other checks failed, use first result by year, setting KinopoiskProviderId to {Id} ({Name})", kinopoiskId, info.Name);
                return (true, kinopoiskId);
            }

            if (0 < candidates_by_type.Count)
            {
                var kinopoiskId = candidates_by_type.First().FilmId;
                _logger.LogDebug("All other checks failed, use first result by type, setting KinopoiskProviderId to {Id} ({Name})", kinopoiskId, info.Name);
                return (true, kinopoiskId);
            }

            if (0 < candidates.Length)
            {
                var kinopoiskId = candidates.First().FilmId;
                _logger.LogDebug("All other checks failed, use first result, setting KinopoiskProviderId to {Id} ({Name})", kinopoiskId, info.Name);
                return (true, kinopoiskId);
            }

            _logger.LogDebug("Suitable result not found");
            return (false, 0);
        }

        public async Task<(bool IsSuccess, int ProviderId)> TryResolveByImdbMatch(T info, ICollection<FilmSearchResponse_films> candidates, CancellationToken? ct = null)
        {
            if (info.TryGetProviderId(MetadataProvider.Imdb, out var imdbId))
            {
                _logger.LogDebug("Trying to find result with ImdbId '{ImdbId}'...", imdbId);
                var index = 0;
                foreach (var candidate in candidates)
                {
                    try
                    {
                        var film = await _kinopoiskApiClient.GetSingleFilm(candidate.FilmId, ct);

                        if (imdbId == film?.ImdbId)
                        {
                            _logger.LogDebug("Found match: {Id} '{Name}', ImdbId '{ImdbId}', setting KinopoiskProviderId to {Id}", candidate.FilmId, film.GetLocalName(), film?.ImdbId, candidate.FilmId);
                            return (true, candidate.FilmId);
                        }

                        _logger.LogDebug("Film {Id} '{Name}' has ImdbId '{ImdbId}', skipping, {Remaining} candidates left...", candidate.FilmId, film.GetLocalName(), film?.ImdbId, candidates.Count - ++index);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error while retrieving film {Id}", candidate.FilmId);
                        continue;
                    }
                }
            }

            return (false, 0);
        }

        public Task<(bool IsSuccess, int ProviderId)> TryResolveBySingleCandidateLeft(T info, ICollection<FilmSearchResponse_films> candidates, CancellationToken? ct = null)
        {
            if (candidates.Count == 1)
            {
                var kinopoiskId = candidates.Single().FilmId;
                _logger.LogDebug("There is single candidate left, setting KinopoiskProviderId to {Id} ({Name})", kinopoiskId, info.Name);
                return Task.FromResult((true, kinopoiskId));
            }

            return Task.FromResult((false, 0));
        }

        public ICollection<FilmSearchResponse_films> FilterByYear(T info, ICollection<FilmSearchResponse_films> candidates)
        {
            if (!info.Year.HasValue)
            {
                _logger.LogDebug("Can't filter by year, no year set in metadata...");
                return Array.Empty<FilmSearchResponse_films>();
            }

            var targetYear = info.Year.Value.ToString();
            var res = candidates.Where(f => f.Year == targetYear).ToArray();
            _logger.LogDebug("Filtered by year {Year}, {Count} results left...", targetYear, res.Length);
            return res;
        }

        /// <summary>
        /// Filters candidates by type (TV_SERIES/TV_SHOW vs FILM) based on the lookup target.
        /// For SeriesInfo we prefer series-like types; for MovieInfo we prefer FILM.
        /// </summary>
        public ICollection<FilmSearchResponse_films> FilterByType(ICollection<FilmSearchResponse_films> candidates)
        {
            var wantSeries = typeof(T) == typeof(SeriesInfo);

            var preferredTypes = wantSeries
                ? new[] { FilmSearchResponse_filmsType.TV_SHOW }
                : new[] { FilmSearchResponse_filmsType.FILM };

            var res = candidates.Where(f => preferredTypes.Contains(f.Type)).ToArray();
            _logger.LogDebug("Filtered by type ({WantSeries}), {Count} results left...", wantSeries ? "series" : "movie", res.Length);
            return res;
        }
    }
}
