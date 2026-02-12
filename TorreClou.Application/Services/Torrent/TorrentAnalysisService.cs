using TorreClou.Core.DTOs.Torrents;
using TorreClou.Core.Enums;
using TorreClou.Core.Interfaces;
using TorreClou.Core.Shared;

namespace TorreClou.Application.Services.Torrent
{
    public class TorrentAnalysisService(
        ITorrentService torrentService) : ITorrentAnalysisService
    {
        public async Task<Result<TorrentAnalysisResponseDto>> AnalyzeTorrentAsync(AnalyzeTorrentRequestDto request, int userId, Stream torrentFile)
        {
            // 1. Validate & Parse Torrent
            var torrentFileValidated = ValidateTorrentFile(torrentFile, request.TorrentFile.FileName);
            if (!torrentFileValidated.IsSuccess)
                return Result<TorrentAnalysisResponseDto>.Failure(torrentFileValidated.Error);

            var torrentInfoResult = await torrentService.GetTorrentInfoFromTorrentFileAsync(torrentFileValidated.Value);
            if (!torrentInfoResult.IsSuccess || string.IsNullOrEmpty(torrentInfoResult.Value.InfoHash))
                return Result<TorrentAnalysisResponseDto>.Failure(ErrorCode.InvalidTorrent, "Failed to parse torrent info.");

            var torrentInfo = torrentInfoResult.Value;

            // 2. Store torrent file (persist .torrent to disk + create/reuse RequestedFile entity)
            torrentFile.Position = 0;
            var torrentStoredResult = await torrentService.FindOrCreateTorrentFile(torrentInfo, userId, torrentFile);

            if (torrentStoredResult.IsFailure)
                return Result<TorrentAnalysisResponseDto>.Failure(ErrorCode.InvalidTorrent, "Failed to save torrent information.");

            // 3. Return analysis response with full file list for frontend selection
            return Result.Success(new TorrentAnalysisResponseDto
            {
                TorrentFileId = torrentStoredResult.Value.Id,
                FileName = torrentStoredResult.Value.FileName,
                InfoHash = torrentStoredResult.Value.InfoHash,
                TotalSizeInBytes = torrentInfo.TotalSize,
                Files = torrentInfo.Files,
                TorrentHealth = torrentInfo.Health,
            });
        }


        // --- Helpers ---

        private Result<Stream> ValidateTorrentFile(Stream torrentFile, string torrentFileName)
        {
            if (torrentFile == null || torrentFile.Length == 0)
                return Result<Stream>.Failure(ErrorCode.Invalid, "No torrent file provided.");

            var fileExtension = Path.GetExtension(torrentFileName).ToLowerInvariant();
            if (fileExtension != ".torrent")
                return Result<Stream>.Failure(ErrorCode.InvalidTorrent, "Invalid file format. Only .torrent files are accepted.");

            return Result<Stream>.Success(torrentFile);
        }
    }
}
