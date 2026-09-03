using TorrenClou.Core.DTOs.Torrents;

namespace TorrenClou.Core.Interfaces
{

    public interface ITrackerScraper
    {
        Task<ScrapeAggregationResult> GetScrapeResultsAsync(string infoHash, IEnumerable<string> trackers);
    }
}