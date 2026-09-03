using TorrenClou.Core.DTOs.Torrents;
using TorrenClou.Core.Entities.Torrents;

namespace TorrenClou.Core.Interfaces
{
    public interface ITorrentService
    {
        Task<TorrentInfoDto> GetTorrentInfoFromTorrentFileAsync(Stream fileStream);
        TorrentInfoDto ParseTorrentFile(Stream fileStream);
        Task<TorrentInfoDto> EnrichWithHealthAsync(TorrentInfoDto torrentInfo);
        Task<RequestedFile> FindOrCreateTorrentFile(TorrentInfoDto torrent, int userId, Stream? fileStream = null);
    }
}
