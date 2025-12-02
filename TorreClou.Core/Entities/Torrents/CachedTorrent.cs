using TorreClou.Core.Enums;
using System.Collections.Generic;
using TorreClou.Core.Entities.Jobs;

namespace TorreClou.Core.Entities.Torrents
{
    public class CachedTorrent : BaseEntity
    {
        public string InfoHash { get; set; } = string.Empty;

        public string MagnetLink { get; set; } = string.Empty;

        public long TotalSizeBytes { get; set; }

        public string? LocalFilePath { get; set; }

        public FileStatus Status { get; set; } = FileStatus.PENDING;

        public int SeedersCountSnapshot { get; set; }

        public DateTime ExpiresAt { get; set; }

        public ICollection<UserJob> Jobs { get; set; } = [];
    }
}