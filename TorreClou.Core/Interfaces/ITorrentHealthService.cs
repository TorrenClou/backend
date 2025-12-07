using TorreClou.Core.DTOs.Torrents;

namespace TorreClou.Core.Interfaces
{
    public interface ITorrentHealthService
    {
        TorrentHealthMeasurements Compute(ScrapeAggregationResult scrape);
    }
}