using TorreClou.Core.DTOs.Torrents;
using TorreClou.Core.Entities.Torrents;
using TorreClou.Core.Interfaces;

namespace TorreClou.Application.Services
{
    public interface ITorrentHealthService
    {
        TorrentHealthMeasurements Compute(ScrapeAggregationResult scrape);
    }
}