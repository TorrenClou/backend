using TorrenClou.Core.DTOs.Torrents;

namespace TorrenClou.Core.Interfaces
{
    public interface ITorrentHealthService
    {
        TorrentHealthMeasurements Compute(ScrapeAggregationResult scrape);
    }
}