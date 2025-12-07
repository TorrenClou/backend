using System.Collections.Generic;
using MonoTorrent;
using TorreClou.Core.DTOs.Torrents;
using TorreClou.Core.Interfaces;
using TorreClou.Core.Shared;

namespace TorreClou.Application.Services
{
    public class TorrentParserService : ITorrentParser
    {
        public Result<TorrentInfoDto> ParseTorrentFile(Stream fileStream)
        {
            try
            {
                var torrent = Torrent.Load(fileStream);

                // ----- Extract Trackers -----
                var trackers = new List<string>();

                // 2) announce-list (tiers)
                if (torrent.AnnounceUrls != null)
                {
                    var list = torrent.AnnounceUrls
                        .SelectMany(tier => tier)
                        .Where(url => !string.IsNullOrWhiteSpace(url))
                        .Distinct()
                        .ToList();

                    trackers.AddRange(list);
                }

                // - نحتفظ فقط بال UDP لأن scraper بتاعك شغال UDP بس
                trackers = trackers
                    .Where(t => t.StartsWith("udp://", StringComparison.OrdinalIgnoreCase))
                    .Distinct()
                    .ToList();


                // ----- Determine InfoHash (v1 only for tracker scrape) -----
                string? hash = torrent.InfoHashes.V1?.ToHex();

                if (string.IsNullOrEmpty(hash))
                {
                    // v2-only torrents cannot be scraped by UDP trackers.
                    return Result<TorrentInfoDto>.Failure(
                        "This torrent has no v1 (SHA1) hash. UDP trackers cannot scrape v2-only torrents."
                    );
                }

                // If no trackers found, fallback to public trackers
                if (trackers.Count == 0)
                {
                    trackers.AddRange(new[]
                    {
                        "udp://tracker.openbittorrent.com:80",
                        "udp://tracker.opentrackr.org:1337/announce",
                        "udp://tracker.coppersurfer.tk:6969/announce",
                        "udp://exodus.desync.com:6969/announce"
                    });
                }

                // ----- Build DTO -----
                var dto = new TorrentInfoDto
                {
                    Name = torrent.Name,
                    InfoHash = hash,
                    TotalSize = torrent.Size,
                    Trackers = trackers,
                    Files = torrent.Files.Select((f, index) => new TorrentFileDto
                    {
                        Index = index,
                        Path = f.Path,
                        Size = f.Length
                    }).ToList()
                };

                return Result.Success(dto);
            }
            catch (Exception ex)
            {
                return Result<TorrentInfoDto>.Failure($"Corrupted torrent file: {ex.Message}");
            }
        }
    }
}
