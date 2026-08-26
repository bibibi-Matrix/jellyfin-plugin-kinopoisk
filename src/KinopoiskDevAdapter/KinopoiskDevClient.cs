using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KinopoiskDevAdapter
{
    /// <summary>
    /// Kinopoisk.dev backend adapter (https://kinopoisk.dev, api v1.4/v1.1).
    /// Implements the unofficial-api client contract by translating responses.
    /// </summary>
    public class KinopoiskDevClient : global::KinopoiskUnofficialInfo.ApiClient.IKinopoiskApiClient
    {
        private const string BaseUrl = "https://api.kinopoisk.dev";
        private const string UserAgent = "Jellyfin-Kinopoisk-Plugin";

        private readonly string _token;
        private readonly ILogger _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

        public KinopoiskDevClient(string token, ILogger<KinopoiskDevClient> logger, IHttpClientFactory httpClientFactory)
        {
            _token = token ?? string.Empty;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        private HttpClient CreateClient()
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            if (!string.IsNullOrWhiteSpace(_token))
                client.DefaultRequestHeaders.Add("X-API-KEY", _token);
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
            return client;
        }

        private async Task<JsonDocument> GetJsonAsync(string pathAndQuery, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_token))
            {
                _logger.LogWarning("KinopoiskDev backend selected but no ApiDevToken configured");
                return JsonDocument.Parse("{}");
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + pathAndQuery);
                using var response = await CreateClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("KinopoiskDev request {Path} failed: HTTP {Status}", pathAndQuery, (int)response.StatusCode);
                    return JsonDocument.Parse("{}");
                }

                var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                return await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                _logger.LogError(e, "KinopoiskDev request {Path} failed", pathAndQuery);
                return JsonDocument.Parse("{}");
            }
        }

        // ---- helpers ----

        private static string Str(JsonElement e, string name)
            => e.ValueKind == JsonValueKind.Object
                && e.TryGetProperty(name, out var v)
                && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static int? Num(JsonElement e, string name)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v))
                return null;
            return v.ValueKind switch
            {
                JsonValueKind.Number when v.TryGetInt32(out var i) => i,
                JsonValueKind.String when int.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var s) => s,
                _ => null
            };
        }

        private static double? Dbl(JsonElement e, string name)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v))
                return null;
            return v.ValueKind switch
            {
                JsonValueKind.Number => v.GetDouble(),
                JsonValueKind.String when double.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
                _ => null
            };
        }

        private static bool? Bool(JsonElement e, string name)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v))
                return null;
            return v.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        private static IEnumerable<JsonElement> Arr(JsonElement e, string name)
        {
            if (e.ValueKind == JsonValueKind.Object
                && e.TryGetProperty(name, out var v)
                && v.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in v.EnumerateArray())
                    yield return item;
            }
        }

        private static string Date10(string iso)
            => string.IsNullOrWhiteSpace(iso) || iso.Length < 10 ? iso : iso.Substring(0, 10);

        // ---- IKinopoiskApiClient ----

        public async Task<global::KinopoiskUnofficialInfo.ApiClient.Film> GetSingleFilm(int filmId, CancellationToken? cancellationToken = null)
        {
            var ct = cancellationToken ?? CancellationToken.None;
            var root = (await GetJsonAsync($"/v1.4/movie/{filmId}", ct).ConfigureAwait(false)).RootElement;
            return MapFilm(root);
        }

        public async Task<global::KinopoiskUnofficialInfo.ApiClient.FilmSearchResponse> SearchByKeyword(string keyword, int page = 1, CancellationToken? cancellationToken = null)
        {
            var ct = cancellationToken ?? CancellationToken.None;
            var query = Uri.EscapeDataString(keyword ?? string.Empty);
            var root = (await GetJsonAsync($"/v1.1/movie/search?query={query}&limit=10&page={Math.Max(1, page)}", ct).ConfigureAwait(false)).RootElement;

            var res = new global::KinopoiskUnofficialInfo.ApiClient.FilmSearchResponse
            {
                SearchFilmsCountResult = Num(root, "total") ?? 0,
                Films = new Collection<global::KinopoiskUnofficialInfo.ApiClient.FilmSearchResponse_films>()
            };

            foreach (var doc in Arr(root, "docs"))
            {
                var film = new global::KinopoiskUnofficialInfo.ApiClient.FilmSearchResponse_films
                {
                    FilmId = Num(doc, "id") ?? 0,
                    NameRu = Str(doc, "name"),
                    NameEn = Str(doc, "enName") ?? Str(doc, "alternativeName"),
                    Year = (Num(doc, "year") ?? 0).ToString(CultureInfo.InvariantCulture),
                    Description = Str(doc, "shortDescription"),
                    FilmLength = (Num(doc, "movieLength") ?? 0).ToString(CultureInfo.InvariantCulture),
                    PosterUrl = Str(Nested(doc, "poster"), "url"),
                    PosterUrlPreview = Str(Nested(doc, "poster"), "previewUrl")
                };
                res.Films.Add(film);
            }

            return res;
        }

        public async Task<global::KinopoiskUnofficialInfo.ApiClient.PersonResponse> GetPerson(int personId, CancellationToken? cancellationToken = null)
        {
            var ct = cancellationToken ?? CancellationToken.None;
            var root = (await GetJsonAsync($"/v1.4/person/{personId}", ct).ConfigureAwait(false)).RootElement;

            return new global::KinopoiskUnofficialInfo.ApiClient.PersonResponse
            {
                PersonId = Num(root, "id") ?? personId,
                NameRu = Str(root, "name"),
                NameEn = Str(root, "enName"),
                Sex = (Str(root, "sex") ?? string.Empty).Equals("male", StringComparison.OrdinalIgnoreCase)
                    ? global::KinopoiskUnofficialInfo.ApiClient.PersonResponseSex.MALE
                    : global::KinopoiskUnofficialInfo.ApiClient.PersonResponseSex.FEMALE,
                PosterUrl = Str(root, "photo"),
                Birthday = Date10(Str(root, "birthday")),
                Death = Date10(Str(root, "death")),
                Birthplace = Str(Nested(root, "birthPlace"), "value")
            };
        }

        public async Task<System.Collections.Generic.ICollection<global::KinopoiskUnofficialInfo.ApiClient.StaffResponse>> GetStaff(int filmId, CancellationToken? cancellationToken = null)
        {
            var root = await GetMovieRoot(filmId, cancellationToken ?? CancellationToken.None).ConfigureAwait(false);
            var list = new Collection<global::KinopoiskUnofficialInfo.ApiClient.StaffResponse>();

            foreach (var p in Arr(root, "persons"))
            {
                list.Add(new global::KinopoiskUnofficialInfo.ApiClient.StaffResponse
                {
                    StaffId = Num(p, "personId") ?? Num(p, "id") ?? 0,
                    NameRu = Str(p, "name"),
                    NameEn = Str(p, "enName"),
                    ProfessionText = Str(p, "description") ?? Str(p, "profession"),
                    ProfessionKey = ToProfessionKey(Str(p, "enProfession")),
                    PosterUrl = Str(p, "photo")
                });
            }

            return list;
        }

        public async Task<global::KinopoiskUnofficialInfo.ApiClient.VideoResponse> GetTrailers(int filmId, CancellationToken? cancellationToken = null)
        {
            var root = await GetMovieRoot(filmId, cancellationToken ?? CancellationToken.None).ConfigureAwait(false);
            var videos = Nested(root, "videos");
            var res = new global::KinopoiskUnofficialInfo.ApiClient.VideoResponse { Items = new Collection<global::KinopoiskUnofficialInfo.ApiClient.VideoResponse_items>() };

            foreach (var t in Arr(videos, "trailers"))
            {
                var url = Str(t, "url");
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                res.Items.Add(new global::KinopoiskUnofficialInfo.ApiClient.VideoResponse_items
                {
                    Url = url,
                    Name = Str(t, "name"),
                    Site = string.Equals(Str(t, "site"), "youtube", StringComparison.OrdinalIgnoreCase)
                        ? global::KinopoiskUnofficialInfo.ApiClient.VideoResponse_itemsSite.YOUTUBE
                        : global::KinopoiskUnofficialInfo.ApiClient.VideoResponse_itemsSite.UNKNOWN
                });
            }

            return res;
        }

        public async Task<global::KinopoiskUnofficialInfo.ApiClient.SeasonResponse> GetSeasons(int filmId, CancellationToken? cancellationToken = null)
        {
            var ct = cancellationToken ?? CancellationToken.None;
            var root = (await GetJsonAsync($"/v1.4/episode?movieId={filmId}&notNullField=airDate&limit=1000", ct).ConfigureAwait(false)).RootElement;

            var seasons = new Dictionary<int, global::KinopoiskUnofficialInfo.ApiClient.Season>();
            foreach (var ep in Arr(root, "docs"))
            {
                var seasonNumber = Num(ep, "seasonNumber") ?? 0;
                if (seasonNumber < 1)
                    continue;

                if (!seasons.TryGetValue(seasonNumber, out var season))
                {
                    season = new global::KinopoiskUnofficialInfo.ApiClient.Season { Number = seasonNumber, Episodes = new Collection<global::KinopoiskUnofficialInfo.ApiClient.Episode>() };
                    seasons[seasonNumber] = season;
                }

                season.Episodes.Add(new global::KinopoiskUnofficialInfo.ApiClient.Episode
                {
                    SeasonNumber = seasonNumber,
                    EpisodeNumber = Num(ep, "episodeNumber") ?? 0,
                    NameRu = Str(ep, "name"),
                    NameEn = Str(ep, "enName"),
                    Synopsis = Str(ep, "description"),
                    ReleaseDate = Date10(Str(ep, "airDate")) ?? string.Empty
                });
            }

            var seasonList = new System.Collections.Generic.List<global::KinopoiskUnofficialInfo.ApiClient.Season>();
            foreach (var key in seasons.Keys)
                seasonList.Add(seasons[key]);

            return new global::KinopoiskUnofficialInfo.ApiClient.SeasonResponse { Items = new Collection<global::KinopoiskUnofficialInfo.ApiClient.Season>(seasonList), Total = seasons.Count };
        }

        public Task<global::KinopoiskUnofficialInfo.ApiClient.FactResponse> GetFacts(int filmId, CancellationToken? cancellationToken = null)
            => Task.FromResult(new global::KinopoiskUnofficialInfo.ApiClient.FactResponse());

        public Task<global::KinopoiskUnofficialInfo.ApiClient.FilmFrameResponse> GetFrames(int filmId, CancellationToken? cancellationToken = null)
            => Task.FromResult(new global::KinopoiskUnofficialInfo.ApiClient.FilmFrameResponse());

        public async Task<global::KinopoiskUnofficialInfo.ApiClient.FilmImagesResponse> GetImages(int filmId, CancellationToken? cancellationToken = null)
        {
            var root = await GetMovieRoot(filmId, cancellationToken ?? CancellationToken.None).ConfigureAwait(false);
            var res = new global::KinopoiskUnofficialInfo.ApiClient.FilmImagesResponse();

            foreach (var b in Arr(root, "backdrops"))
            {
                var url = Str(b, "url");
                if (!string.IsNullOrWhiteSpace(url))
                    res.Items.Add(new global::KinopoiskUnofficialInfo.ApiClient.FilmImage { ImageUrl = url, ImagePreviewUrl = Str(b, "previewUrl") });
            }

            res.Total = res.Items.Count;
            return res;
        }

        public async Task<global::KinopoiskUnofficialInfo.ApiClient.DistributionResponse> GetDistributions(int filmId, CancellationToken? cancellationToken = null)
        {
            var root = await GetMovieRoot(filmId, cancellationToken ?? CancellationToken.None).ConfigureAwait(false);
            var premiere = Nested(root, "premiere");
            var worldDate = Date10(Str(premiere, "world"));
            var res = new global::KinopoiskUnofficialInfo.ApiClient.DistributionResponse();

            if (!string.IsNullOrWhiteSpace(worldDate))
            {
                res.Items.Add(new global::KinopoiskUnofficialInfo.ApiClient.Distribution
                {
                    Type = global::KinopoiskUnofficialInfo.ApiClient.DistributionType.WORLD_PREMIER,
                    Date = worldDate
                });
            }

            res.Total = res.Items.Count;
            return res;
        }

        // ---- movie mapping ----

        private readonly Dictionary<int, JsonElement> _movieCache = new();
        private readonly object _cacheLock = new();

        private async Task<JsonElement> GetMovieRoot(int filmId, CancellationToken ct)
        {
            lock (_cacheLock)
            {
                if (_movieCache.TryGetValue(filmId, out var cached))
                    return cached;
            }

            var root = (await GetJsonAsync($"/v1.4/movie/{filmId}", ct).ConfigureAwait(false)).RootElement.Clone();

            lock (_cacheLock)
            {
                _movieCache[filmId] = root;
                if (_movieCache.Count > 256)
                    _movieCache.Clear();
            }

            return root;
        }

        private global::KinopoiskUnofficialInfo.ApiClient.Film MapFilm(JsonElement root)
        {
            var film = new global::KinopoiskUnofficialInfo.ApiClient.Film
            {
                KinopoiskId = Num(root, "id") ?? 0,
                NameRu = Str(root, "name"),
                NameEn = Str(root, "enName"),
                NameOriginal = Str(root, "alternativeName"),
                ImdbId = Str(Nested(root, "externalId"), "imdb"),
                Year = Num(root, "year") ?? 0,
                Slogan = Str(root, "slogan"),
                Description = Str(root, "description"),
                ShortDescription = Str(root, "shortDescription"),
                FilmLength = (Num(root, "movieLength") ?? 0).ToString(CultureInfo.InvariantCulture),
                RatingMpaa = Str(root, "mpaa"),
                PosterUrl = Str(Nested(root, "poster"), "url"),
                PosterUrlPreview = Str(Nested(root, "poster"), "previewUrl")
            };

            var ageRating = Num(root, "ageRating");
            if (ageRating.HasValue)
                film.RatingAgeLimits = ageRating.Value.ToString(CultureInfo.InvariantCulture);

            var rating = Nested(root, "rating");
            film.RatingKinopoisk = (float?)Dbl(rating, "kp") ?? 0f;
            film.RatingImdb = (float?)Dbl(rating, "imdb") ?? 0f;
            film.RatingFilmCritics = (float?)Dbl(rating, "filmCritics") ?? 0f;
            film.RatingRfCritics = (float?)Dbl(rating, "rfCritics") ?? 0f;

            var typeStr = Str(root, "type") ?? string.Empty;
            film.Type =
                typeStr.Contains("series", StringComparison.OrdinalIgnoreCase) ? global::KinopoiskUnofficialInfo.ApiClient.FilmType.TV_SERIES :
                typeStr.Equals("tv-show", StringComparison.OrdinalIgnoreCase) ? global::KinopoiskUnofficialInfo.ApiClient.FilmType.TV_SHOW :
                global::KinopoiskUnofficialInfo.ApiClient.FilmType.FILM;
            film.Serial = film.Type == global::KinopoiskUnofficialInfo.ApiClient.FilmType.TV_SERIES;

            var releaseYears = First(Arr(root, "releaseYears"));
            if (!releaseYears.Equals(default))
            {
                film.StartYear = Num(releaseYears, "start") ?? 0;
                film.EndYear = Num(releaseYears, "end") ?? 0;
            }

            foreach (var c in Arr(root, "countries"))
            {
                var name = Str(c, "name");
                if (!string.IsNullOrEmpty(name))
                    film.Countries.Add(new global::KinopoiskUnofficialInfo.ApiClient.Country { Country1 = name });
            }

            foreach (var g in Arr(root, "genres"))
            {
                var name = Str(g, "name");
                if (!string.IsNullOrEmpty(name))
                    film.Genres.Add(new global::KinopoiskUnofficialInfo.ApiClient.Genre { Genre1 = name });
            }

            return film;
        }

        private static JsonElement Nested(JsonElement e, string name)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) ? v : default;

        private static JsonElement First(IEnumerable<JsonElement> source)
        {
            foreach (var item in source)
                return item;
            return default;
        }

        private static global::KinopoiskUnofficialInfo.ApiClient.StaffResponseProfessionKey ToProfessionKey(string enProfession)
        {
            return (enProfession ?? string.Empty) switch
            {
                "actor" => global::KinopoiskUnofficialInfo.ApiClient.StaffResponseProfessionKey.ACTOR,
                "director" => global::KinopoiskUnofficialInfo.ApiClient.StaffResponseProfessionKey.DIRECTOR,
                "writer" => global::KinopoiskUnofficialInfo.ApiClient.StaffResponseProfessionKey.WRITER,
                "producer" => global::KinopoiskUnofficialInfo.ApiClient.StaffResponseProfessionKey.PRODUCER,
                "composer" => global::KinopoiskUnofficialInfo.ApiClient.StaffResponseProfessionKey.COMPOSER,
                "editor" => global::KinopoiskUnofficialInfo.ApiClient.StaffResponseProfessionKey.EDITOR,
                "voice-director" or "translator" => global::KinopoiskUnofficialInfo.ApiClient.StaffResponseProfessionKey.TRANSLATOR,
                "operator" => global::KinopoiskUnofficialInfo.ApiClient.StaffResponseProfessionKey.OPERATOR,
                _ => global::KinopoiskUnofficialInfo.ApiClient.StaffResponseProfessionKey.UNKNOWN
            };
        }
    }
}
