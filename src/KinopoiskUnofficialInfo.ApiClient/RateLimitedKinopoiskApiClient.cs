using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KinopoiskUnofficialInfo.ApiClient
{
    /// <summary>
    /// Decorator that limits the number of requests per second to the API.
    /// </summary>
    public class RateLimitedKinopoiskApiClient : IKinopoiskApiClient
    {
        private readonly IKinopoiskApiClient _innerClient;
        private readonly SlidingWindowRateLimiter _rateLimiter;
        private readonly ILogger<RateLimitedKinopoiskApiClient> _logger;

        public RateLimitedKinopoiskApiClient(IKinopoiskApiClient innerClient, int maxRequestsPerSecond, ILogger<RateLimitedKinopoiskApiClient> logger)
        {
            _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _rateLimiter = new SlidingWindowRateLimiter(maxRequestsPerSecond > 0 ? maxRequestsPerSecond : 8);
        }

        public async Task<PersonResponse> GetPerson(int personId, CancellationToken? cancellationToken = null)
            => await Invoke(c => _innerClient.GetPerson(personId, c), cancellationToken);

        public async Task<Film> GetSingleFilm(int filmId, CancellationToken? cancellationToken = null)
            => await Invoke(c => _innerClient.GetSingleFilm(filmId, c), cancellationToken);

        public async Task<ICollection<StaffResponse>> GetStaff(int filmId, CancellationToken? cancellationToken = null)
            => await Invoke(c => _innerClient.GetStaff(filmId, c), cancellationToken);

        public async Task<VideoResponse> GetTrailers(int filmId, CancellationToken? cancellationToken = null)
            => await Invoke(c => _innerClient.GetTrailers(filmId, c), cancellationToken);

        public async Task<FilmSearchResponse> SearchByKeyword(string keyword, int page = 1, CancellationToken? cancellationToken = null)
            => await Invoke(c => _innerClient.SearchByKeyword(keyword, page, c), cancellationToken);

        public async Task<SeasonResponse> GetSeasons(int filmId, CancellationToken? cancellationToken = null)
            => await Invoke(c => _innerClient.GetSeasons(filmId, c), cancellationToken);

        public async Task<FilmImagesResponse> GetImages(int filmId, CancellationToken? cancellationToken = null)
            => await Invoke(c => _innerClient.GetImages(filmId, c), cancellationToken);

        private async Task<T> Invoke<T>(Func<CancellationToken?, Task<T>> call, CancellationToken? cancellationToken, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            var ct = cancellationToken ?? CancellationToken.None;
            await _rateLimiter.WaitForSlotAsync(ct).ConfigureAwait(false);
            if (_logger.IsEnabled(LogLevel.Trace))
                _logger.LogTrace($"{memberName} passed rate limiter");
            return await call(cancellationToken).ConfigureAwait(false);
        }

        internal sealed class SlidingWindowRateLimiter
        {
            private readonly object _syncRoot = new();
            private readonly Queue<DateTime> _timestamps = new();
            private readonly int _maxRequests;

            public SlidingWindowRateLimiter(int maxRequests)
            {
                _maxRequests = Math.Max(1, maxRequests);
            }

            public async Task WaitForSlotAsync(CancellationToken cancellationToken)
            {
                while (true)
                {
                    TimeSpan wait;
                    lock (_syncRoot)
                    {
                        var now = DateTime.UtcNow;
                        while (_timestamps.Count > 0 && (now - _timestamps.Peek()).TotalMilliseconds >= 1000.0)
                            _timestamps.Dequeue();

                        if (_timestamps.Count < _maxRequests)
                        {
                            _timestamps.Enqueue(now);
                            return;
                        }

                        wait = _timestamps.Peek().Add(TimeSpan.FromMilliseconds(1050.0)) - now;
                    }

                    if (wait < TimeSpan.FromMilliseconds(10.0))
                        wait = TimeSpan.FromMilliseconds(10.0);

                    await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
