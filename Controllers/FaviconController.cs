using FaviconScraper.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using System.Net;

namespace FaviconScraper.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("fixed")]
    public class FaviconController : ControllerBase
    {
        private readonly IFaviconService _faviconService;
        private readonly ILogger<FaviconController> _logger;

        public FaviconController(IFaviconService faviconService, ILogger<FaviconController> logger)
        {
            _faviconService = faviconService;
            _logger = logger;
        }

        [HttpGet("{size:int}/{*url}")]
        public async Task<IActionResult> GetWithSize(int size, string url)
        {
            return await GetFaviconInternal(url, size);
        }

        [HttpGet("{*url}")]
        public async Task<IActionResult> Get(string url)
        {
            return await GetFaviconInternal(url, null);
        }

        private async Task<IActionResult> GetFaviconInternal(string url, int? size)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                _logger.LogWarning("Request received with empty URL.");
                return BadRequest("URL is required.");
            }

            url = Uri.UnescapeDataString(url);

            // Url decoding might be needed depending on how the client sends it, 
            // but AspNetCore usually decodes the path string. 
            // However, with catch-all *url, let's ensure it's treated correctly.
            // If the user sends encoded slashes %2F, they might stay encoded or be decoded depending on configuration.
            // For now, let's assume standard behavior.

            // Basic SSRF Prevention
            if (url.Contains("localhost") || url.Contains("127.0.0.1") || url.Contains("::1"))
            {
                _logger.LogWarning("Blocked potentially restricted URL: {Url}", url);
                return BadRequest("Restricted URL.");
            }

            try
            {
                _logger.LogInformation("Received request for {Url} (Size: {Size})", url, size);
                var faviconBytes = await _faviconService.GetFaviconAsync(url, size);

                if (faviconBytes == null || faviconBytes.Length == 0)
                {
                    _logger.LogInformation("Favicon not found for {Url}", url);
                    return NotFound("Favicon not found.");
                }

                _logger.LogInformation("Successfully returned favicon for {Url} ({Bytes} bytes)", url, faviconBytes.Length);
                return File(faviconBytes, "image/png");
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Bad request argument for {Url}", url);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error fetching favicon for {Url}", url);
                return StatusCode(500, "An error occurred while fetching the favicon.");
            }
        }
    }
}
