using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KinopoiskUnofficialInfo.ApiClient;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk
{
    /// <summary>
    /// Wraps IKinopoiskApiClient and allows swapping the inner client at runtime
    /// without restarting the server.
    /// </summary>
    public class SwitchableKinopoiskClient : IKinopoiskApiClient
    {
        private volatile IKinopoiskApiClient _inner;
        private readonly ILogger<SwitchableKinopoiskClient> _logger;

        public SwitchableKinopoiskClient(IKinopoiskApiClient initialClient, ILogger<SwitchableKinopoiskClient> logger)
        {
            _inner = initialClient ?? throw new ArgumentNullException(nameof(initialClient));
            _logger = logger;
        }

        public IKinopoiskApiClient Inner => _inner;

        public void SwitchTo(IKinopoiskApiClient newClient)
        {
            _logger.LogInformation("Switching kinopoisk backend from {OldType} to {NewType}",
                _inner.GetType().Name, newClient.GetType().Name);
            _inner = newClient;
        }

        public Task<PersonResponse> GetPerson(int personId, CancellationToken? cancellationToken = null)
            => _inner.GetPerson(personId, cancellationToken);

        public Task<Film> GetSingleFilm(int filmId, CancellationToken? cancellationToken = null)
            => _inner.GetSingleFilm(filmId, cancellationToken);

        public Task<ICollection<StaffResponse>> GetStaff(int filmId, CancellationToken? cancellationToken = null)
            => _inner.GetStaff(filmId, cancellationToken);

        public Task<VideoResponse> GetTrailers(int filmId, CancellationToken? cancellationToken = null)
            => _inner.GetTrailers(filmId, cancellationToken);

        public Task<FilmSearchResponse> SearchByKeyword(string keyword, int page = 1, CancellationToken? cancellationToken = null)
            => _inner.SearchByKeyword(keyword, page, cancellationToken);

        public Task<FilmImagesResponse> GetImages(int filmId, CancellationToken? cancellationToken = null)
            => _inner.GetImages(filmId, cancellationToken);

        public Task<SeasonResponse> GetSeasons(int filmId, CancellationToken? cancellationToken = null)
            => _inner.GetSeasons(filmId, cancellationToken);

        public Task<FactResponse> GetFacts(int filmId, CancellationToken? cancellationToken = null)
            => _inner.GetFacts(filmId, cancellationToken);

        public Task<FilmFrameResponse> GetFrames(int filmId, CancellationToken? cancellationToken = null)
            => _inner.GetFrames(filmId, cancellationToken);

        public Task<DistributionResponse> GetDistributions(int filmId, CancellationToken? cancellationToken = null)
            => _inner.GetDistributions(filmId, cancellationToken);
    }
}
