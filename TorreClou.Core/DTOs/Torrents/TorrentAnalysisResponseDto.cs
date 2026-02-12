namespace TorreClou.Core.DTOs.Torrents
{
    public record TorrentAnalysisResponseDto
    {
        public int TorrentFileId { get; init; }
        public string FileName { get; init; } = string.Empty;
        public string InfoHash { get; init; } = string.Empty;
        public long TotalSizeInBytes { get; init; }
        public List<TorrentFileDto> Files { get; init; } = new();
        public TorrentHealthMeasurements TorrentHealth { get; init; } = null!;
    }
}
