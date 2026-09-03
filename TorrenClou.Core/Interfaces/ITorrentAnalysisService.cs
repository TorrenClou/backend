using TorrenClou.Core.DTOs.Torrents;

namespace TorrenClou.Core.Interfaces
{
    public interface ITorrentAnalysisService
    {
        Task<TorrentAnalysisResponseDto> AnalyzeTorrentAsync(
            AnalyzeTorrentRequestDto request,
            int userId,
            Stream torrentFile);
    }
}
