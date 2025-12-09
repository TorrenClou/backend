using MonoTorrent;
using TorreClou.Core.DTOs.Torrents;
using TorreClou.Core.Entities.Torrents;
using TorreClou.Core.Interfaces;
using TorreClou.Core.Shared;
using TorreClou.Core.Specifications;
using RequestedFile = TorreClou.Core.Entities.Torrents.RequestedFile;

namespace TorreClou.Application.Services.Torrent
{
    public class TorrentService(IUnitOfWork unitOfWork, ITrackerScraper trackerScraper, IBlobStorageService blobStorageService) : ITorrentService
    {

        public async Task<Result<TorrentInfoDto>> GetTorrentInfoFromTorrentFileAsync(Stream fileStream)
        {
            try
            {
                var torrent = MonoTorrent.Torrent.Load(fileStream);

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

                trackers = trackers
                    .Where(t => t.StartsWith("udp://", StringComparison.OrdinalIgnoreCase))
                    .Distinct()
                    .ToList();


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
                var scrape = await trackerScraper.GetScrapeResultsAsync(
              hash,
              trackers
          );
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
                    }).ToList(),
                    ScrapeResult =scrape
                  
                };

                return Result.Success(dto);
            }
            catch (Exception ex)
            {
                return Result<TorrentInfoDto>.Failure($"Corrupted torrent file: {ex.Message}");
            }
        }


        public async Task<Result<RequestedFile>> FindOrCreateTorrentFile(RequestedFile torrent, Stream? fileStream = null)
        {
            var searchCriteria = new BaseSpecification<RequestedFile>(t => t.InfoHash == torrent.InfoHash && t.UploadedByUserId == torrent.UploadedByUserId);

            var existingTorrent = await unitOfWork.Repository<RequestedFile>().GetEntityWithSpec(searchCriteria);

            if (existingTorrent != null)
            {
                // If file exists but has no DirectUrl and we have a stream, upload it
                if (string.IsNullOrEmpty(existingTorrent.DirectUrl) && fileStream != null)
                {
                    fileStream.Position = 0;
                    var uploadResult = await blobStorageService.UploadAsync(
                        fileStream,
                        $"{existingTorrent.InfoHash}.torrent",
                        "application/x-bittorrent"
                    );

                    if (uploadResult.IsSuccess)
                    {
                        existingTorrent.DirectUrl = uploadResult.Value;
                        await unitOfWork.Complete();
                    }
                }

                return Result.Success(existingTorrent);
            }

            // Upload file to blob storage if stream is provided
            if (fileStream != null)
            {
                fileStream.Position = 0;
                var uploadResult = await blobStorageService.UploadAsync(
                    fileStream,
                    $"{torrent.InfoHash}.torrent",
                    "application/x-bittorrent"
                );

                if (uploadResult.IsSuccess)
                {
                    torrent.DirectUrl = uploadResult.Value;
                }
            }

            unitOfWork.Repository<RequestedFile>().Add(torrent);
            await unitOfWork.Complete();

            return Result.Success(torrent);
        }
    }
}