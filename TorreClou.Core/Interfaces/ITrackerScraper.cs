using TorreClou.Application.Services;
using TorreClou.Core.DTOs.Torrents;

namespace TorreClou.Core.Interfaces
{

    public interface ITrackerScraper
    {
        Task<ScrapeAggregationResult> GetScrapeResultsAsync(string infoHash, IEnumerable<string> trackers);
    }
}