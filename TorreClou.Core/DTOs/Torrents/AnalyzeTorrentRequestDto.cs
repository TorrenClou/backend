using Microsoft.AspNetCore.Http;

namespace TorreClou.Core.DTOs.Torrents
{
    public class AnalyzeTorrentRequestDto
    {
        public IFormFile TorrentFile { get; set; } = null!;
    }
}
