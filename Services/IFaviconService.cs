using System.Threading.Tasks;

namespace FaviconScraper.Api.Services
{
    public interface IFaviconService
    {
        Task<byte[]> GetFaviconAsync(string url, int? size);
    }
}
