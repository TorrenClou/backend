using Microsoft.AspNetCore.Http;

namespace TorrenClou.Core.DTOs.Torrents
{
    public class AnalyzeTorrentRequestDto
    {
        public IFormFile TorrentFile { get; set; } = null!;
    }
}
