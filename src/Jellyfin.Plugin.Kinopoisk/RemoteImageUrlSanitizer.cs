using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Kinopoisk
{
    public class RemoteImageUrlSanitizer
    {
        private const int MaxRedirectHops = 10;
        private readonly HttpClient _httpClient;

        public RemoteImageUrlSanitizer(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new System.ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
        /// Follows redirects manually and returns the final direct url,
        /// or null when the url points to a placeholder or is unreachable.
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

                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
                    using var response = await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead);

                    if ((int)response.StatusCode <= 299)
                        return currentUrl;
                    else if (response.Headers.Location != null)
                    {
                        currentUrl = response.Headers.Location.IsAbsoluteUri
                            ? response.Headers.Location.AbsoluteUri
                            : new Uri(new Uri(currentUrl), response.Headers.Location).AbsoluteUri;
                        continue;
                    }
                    else
                        return null;
                }
                catch (Exception)
                {
                    return null;
                }
            }

            return null;
        }
    }
}
