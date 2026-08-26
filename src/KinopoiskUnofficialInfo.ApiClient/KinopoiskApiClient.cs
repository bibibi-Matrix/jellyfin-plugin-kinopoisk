using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KinopoiskUnofficialInfo.ApiClient
{
    public class KinopoiskApiClient : IKinopoiskApiClient
    {
        private const string BaseUrl = "https://kinopoiskapiunofficial.tech";

        private readonly string _apiToken;
        private readonly ILogger<KinopoiskApiClient> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly Client _apiClient;

        public KinopoiskApiClient(string apiToken, ILogger<KinopoiskApiClient> logger, IHttpClientFactory httpClientFactory)
        {
            if (string.IsNullOrEmpty(apiToken))
            {
                throw new System.ArgumentException($"'{nameof(apiToken)}' cannot be null or empty.", nameof(apiToken));
            }

            _apiToken = apiToken;
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new System.ArgumentNullException(nameof(httpClientFactory));

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("X-API-KEY", _apiToken);
            _apiClient = new Client(httpClient);
        }

        private async Task<T> Invoke<T>(Func<CancellationToken, Task<T>> method, CancellationToken? ct, [CallerMemberName] string memberName = "")
        {
            try
            {
                _logger.LogDebug($"{memberName} request starting...");
                var res = await method.Invoke(ct ?? CancellationToken.None);
                _logger.LogDebug($"{memberName} request complete successfully");
                return res;
            }
            catch (ApiException e)
            {
                _logger.LogError($"Received non-success result status code {e.StatusCode} from Kinopoisk API, response content is:\n{e.Response}");
                throw;
            }
        }

        public Task<Film> GetSingleFilm(int filmId, CancellationToken? cancellationToken = null)
            => Invoke((ct) => _apiClient.FilmsAsync(filmId, ct), cancellationToken);

        public Task<ICollection<StaffResponse>> GetStaff(int filmId, CancellationToken? cancellationToken = null)
            => Invoke((ct) => _apiClient.StaffAllAsync(filmId, ct), cancellationToken);

        public Task<FilmSearchResponse> SearchByKeyword(string keyword, int page = 1, CancellationToken? cancellationToken = null)
            => Invoke((ct) => _apiClient.SearchByKeywordAsync(keyword, null, ct), cancellationToken);

        public Task<PersonResponse> GetPerson(int personId, CancellationToken? cancellationToken = null)
            => Invoke((ct) => _apiClient.StaffAsync(personId, ct), cancellationToken);

        public Task<VideoResponse> GetTrailers(int filmId, CancellationToken? cancellationToken = null)
        {
            return Invoke(async (ct) => {
                try {
                    return await _apiClient.VideosAsync(filmId, ct);
                } catch (ApiException e)
                {
                    if (e.StatusCode == 404)
                        return new VideoResponse();
                    throw;
                }
            }, cancellationToken);
        }

        public async Task<FilmImagesResponse> GetImages(int filmId, CancellationToken? cancellationToken = null)
        {
            var res = new FilmImagesResponse();

            // Gallery images (stills etc.)
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                httpClient.DefaultRequestHeaders.Add("X-API-KEY", _apiToken);

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/v2.2/films/{filmId}/images?type=STILL&page=1");
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken ?? CancellationToken.None).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken ?? CancellationToken.None).ConfigureAwait(false);
                var parsed = await JsonSerializer.DeserializeAsync<FilmImagesResponse>(stream, JsonOpts, cancellationToken ?? CancellationToken.None).ConfigureAwait(false);
                if (parsed?.Items != null)
                    foreach (var item in parsed.Items)
                        res.Items.Add(item);
                res.Total = res.Items.Count;
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogError($"Images request for film {filmId} failed: {e.Message}");
            }

            // Logo and cover from film meta (v2.2 fields missing in the generated v2.1 model)
            try
            {
                var httpClient2 = _httpClientFactory.CreateClient();
                httpClient2.Timeout = TimeSpan.FromSeconds(30);
                httpClient2.DefaultRequestHeaders.Add("X-API-KEY", _apiToken);

                using var metaRequest = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/v2.2/films/{filmId}");
                using var metaResponse = await httpClient2.SendAsync(metaRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken ?? CancellationToken.None).ConfigureAwait(false);
                metaResponse.EnsureSuccessStatusCode();
                using var metaStream = await metaResponse.Content.ReadAsStreamAsync(cancellationToken ?? CancellationToken.None).ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(metaStream, cancellationToken: cancellationToken ?? CancellationToken.None).ConfigureAwait(false);

                AddIfPresent(res, doc.RootElement, "logoUrl", "logo");
                AddIfPresent(res, doc.RootElement, "coverUrl", "cover");
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogDebug($"Film meta request for film {filmId} failed: {e.Message}");
            }

            return res;
        }

        private void AddIfPresent(FilmImagesResponse res, JsonElement root, string fieldName, string kind)
        {
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty(fieldName, out var v)
                && v.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(v.GetString()))
            {
                res.Items.Add(new FilmImage { ImageUrl = v.GetString(), Kind = kind });
                res.Total = res.Items.Count;
            }
        }

        public Task<SeasonResponse> GetSeasons(int filmId, CancellationToken? cancellationToken = null)
        {
            return Invoke(async (ct) => {
                try {
                    return await _apiClient.SeasonsAsync(filmId, ct);
                } catch (ApiException e)
                {
                    if (e.StatusCode == 404)
                        return new SeasonResponse();
                    throw;
                }
            }, cancellationToken);
        }

        public Task<FactResponse> GetFacts(int filmId, CancellationToken? cancellationToken = null)
            => Invoke((ct) => _apiClient.FactsAsync(filmId, ct), cancellationToken);

        public Task<FilmFrameResponse> GetFrames(int filmId, CancellationToken? cancellationToken = null)
            => Invoke((ct) => _apiClient.FramesAsync(filmId, ct), cancellationToken);

        public Task<DistributionResponse> GetDistributions(int filmId, CancellationToken? cancellationToken = null)
            => Invoke((ct) => _apiClient.DistributionsAsync(filmId, ct), cancellationToken);

        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    }
}
