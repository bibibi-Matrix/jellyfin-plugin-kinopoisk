using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Kinopoisk
{
    public class RemoteImageUrlSanitizer
    {
        private const int MaxRedirectHops = 10;
        private const int MaxNetworkRetries = 2;

        private readonly HttpClient _httpClient;

        public RemoteImageUrlSanitizer(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new System.ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
        /// Follows redirects manually and returns the final direct url,
        /// or null when the url points to a placeholder or is rejected by the server.
        /// On transient network failures returns the original url as a fallback.
        /// Never throws.
        /// </summary>
        public async Task<string> SanitizeRemoteImageUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            var currentUrl = url;
            for (var hop = 0; hop < MaxRedirectHops; hop++)
            {
                if (currentUrl.Contains("no-poster"))
                    return null;

                for (var attempt = 0; ; attempt++)
                {
                    try
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
                        using var response = await _httpClient.SendAsync(
                            request,
                            HttpCompletionOption.ResponseHeadersRead);

                        if ((int)response.StatusCode <= 299)
                            return currentUrl;

                        if (response.Headers.Location != null)
                        {
                            currentUrl = response.Headers.Location.IsAbsoluteUri
                                ? response.Headers.Location.AbsoluteUri
                                : new Uri(new Uri(currentUrl), response.Headers.Location).AbsoluteUri;
                            break;
                        }

                        // Definitive server-side rejection (403/404/etc.)
                        return null;
                    }
                    catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
                    {
                        if (attempt < MaxNetworkRetries)
                        {
                            await Task.Delay(300 * (attempt + 1));
                            continue;
                        }

                        // Transient network/DNS failure: let the caller try the url itself,
                        // its client follows redirects automatically.
                        return url;
                    }
                }
            }

            return null;
        }
    }
}
