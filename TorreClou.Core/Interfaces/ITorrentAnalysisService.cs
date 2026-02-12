using TorreClou.Core.DTOs.Torrents;
using TorreClou.Core.Shared;

namespace TorreClou.Core.Interfaces
{
    public interface ITorrentAnalysisService
    {
        Task<Result<TorrentAnalysisResponseDto>> AnalyzeTorrentAsync(
            AnalyzeTorrentRequestDto request,
            int userId,
            Stream torrentFile);
    }
}
