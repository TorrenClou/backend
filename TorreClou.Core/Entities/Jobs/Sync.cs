using TorreClou.Core.Enums;

namespace TorreClou.Core.Entities.Jobs
{
    /// <summary>
    /// Represents an S3 sync operation for a torrent job.
    /// Uses SyncStatus enum (not JobStatus) and has its own recovery service (SyncRecoveryService).
    /// </summary>
    public class Sync : BaseEntity
    {
        public int JobId { get; set; }
        public UserJob UserJob { get; set; } = null!;
        public SyncStatus Status { get; set; }
        public string? LocalFilePath { get; set; } // Block storage path
        public string? S3KeyPrefix { get; set; } // e.g., "torrents/{jobId}"
        public long TotalBytes { get; set; }
        public long BytesSynced { get; set; }
        public int FilesTotal { get; set; }
        public int FilesSynced { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int RetryCount { get; set; }
        public DateTime? NextRetryAt { get; set; }
        
        // Navigation property for file-level progress
        public ICollection<S3SyncProgress> FileProgress { get; set; } = new List<S3SyncProgress>();

        /// <summary>
        /// Last heartbeat timestamp from the worker processing this sync.
        /// Used by SyncRecoveryService to detect stale/orphaned jobs.
        /// </summary>
        public DateTime? LastHeartbeat { get; set; }

        /// <summary>
        /// Hangfire job ID for state reconciliation and idempotency.
        /// </summary>
        public string? HangfireJobId { get; set; }

        /// <summary>
        /// Status change history for this sync, providing a complete audit trail.
        /// </summary>
        public ICollection<SyncStatusHistory> StatusHistory { get; set; } = new List<SyncStatusHistory>();
    }
}
