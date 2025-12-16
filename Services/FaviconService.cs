using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace FaviconScraper.Api.Services
{
    public class FaviconService : IFaviconService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly ILogger<FaviconService> _logger;

        public FaviconService(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<FaviconService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _logger = logger;
        }

        public async Task<byte[]> GetFaviconAsync(string url, int? size)
        {
            // 1. Root Domain Enforcement
            // Handle Kestrel/Proxy path normalization where // is merged to /
            if (url.StartsWith("https:/", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = url.Replace("https:/", "https://");
            }
            else if (url.StartsWith("http:/", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                url = url.Replace("http:/", "http://");
            }
            // Fallback for missing scheme
            else if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                _logger.LogWarning("Invalid URL provided: {Url}", url);
                throw new ArgumentException("Invalid URL");
            }
            var rootUrl = uri.GetLeftPart(UriPartial.Authority);
            
            // 2. Caching Key
            string cacheKey = $"favicon_{rootUrl}_{size}";

            if (_cache.TryGetValue(cacheKey, out byte[] cachedImage))
            {
                _logger.LogInformation("Cache hit for {Url} (Size: {Size})", rootUrl, size);
                return cachedImage;
            }

            _logger.LogInformation("Cache miss for {Url}. Fetching...", rootUrl);

            // 3. Fetch Favicon
            var imageBytes = await FetchFaviconBytesAsync(rootUrl);

            if (imageBytes == null || imageBytes.Length == 0)
            {
                _logger.LogWarning("Could not find favicon for {Url}", rootUrl);
                return null; // Or return a default favicon
            }

            // 4. Resize if needed
            if (size.HasValue && size.Value > 0)
            {
                _logger.LogInformation("Resizing favicon for {Url} to {Size}px", rootUrl, size);
                imageBytes = ResizeImage(imageBytes, size.Value);
            }

            // 5. Cache Result (e.g., 24 hours)
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromHours(24));
            
            _cache.Set(cacheKey, imageBytes, cacheEntryOptions);

            return imageBytes;
        }

        private async Task<byte[]> FetchFaviconBytesAsync(string rootUrl)
        {
            var client = _httpClientFactory.CreateClient();
            // User-Agent to act like a browser
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3");

            try 
            {
                // try to find in HTML first
                var html = await client.GetStringAsync(rootUrl);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var iconLinks = doc.DocumentNode.SelectNodes("//link[contains(@rel, 'icon')]");
                string iconUrl = null;

                if (iconLinks != null)
                {
                    // Prioritize larger icons or apple-touch-icon if available, but for now just pick the first valid one
                    // or look for specific sizes. 
                    // Simple approach: get the href of the first one.
                    foreach(var link in iconLinks)
                    {
                        var href = link.GetAttributeValue("href", null);
                        if (!string.IsNullOrEmpty(href))
                        {
                            iconUrl = href;
                            break; 
                        }
                    }
                }

                if (string.IsNullOrEmpty(iconUrl))
                {
                     // Fallback to /favicon.ico
                     iconUrl = new Uri(new Uri(rootUrl), "/favicon.ico").ToString();
                     _logger.LogDebug("No link tag found. Trying fallback: {FallbackUrl}", iconUrl);
                }
                else 
                {
                    // Handle relative URLs
                    if (Uri.TryCreate(iconUrl, UriKind.Relative, out _))
                    {
                        iconUrl = new Uri(new Uri(rootUrl), iconUrl).ToString();
                    }
                    _logger.LogDebug("Found favicon URL in HTML: {IconUrl}", iconUrl);
                }

                return await client.GetByteArrayAsync(iconUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching favicon from HTML/Link tags for {Url}. Trying root fallback.", rootUrl);
                // Fallback if HTML fetch fails or parsing fails, try root favicon.ico directly
                try
                {
                    var fallbackUrl = new Uri(new Uri(rootUrl), "/favicon.ico").ToString();
                    return await client.GetByteArrayAsync(fallbackUrl);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Failed to fetch favicon from fallback {FallbackUrl}", rootUrl);
                    return null;
                }
            }
        }

        private byte[] ResizeImage(byte[] originalBytes, int targetSize)
        {
            _logger.LogWarning("Resizing image to {TargetSize}px", targetSize);
            try
            {
                using var inputStream = new MemoryStream(originalBytes);
                // SkiaSharp decode
                using var originalBitmap = SKBitmap.Decode(inputStream);
                
                if (originalBitmap == null) 
                {
                    _logger.LogWarning("SkiaSharp failed to decode image bytes.");
                    return originalBytes; // Failed to decode, return original
                }

                // Resizing logic
                if (originalBitmap.Width == targetSize && originalBitmap.Height == targetSize)
                {
                    return originalBytes;
                }

                var info = new SKImageInfo(targetSize, targetSize);
                using var resizedBitmap = originalBitmap.Resize(info, SKFilterQuality.High);
                using var image = SKImage.FromBitmap(resizedBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                _logger.LogWarning("Resized image to {Width}x{Height}", targetSize, targetSize);
                return data.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resizing image.");
                // If resizing fails (e.g. invalid format), return original
                return originalBytes;
            }
        }
    }
}
