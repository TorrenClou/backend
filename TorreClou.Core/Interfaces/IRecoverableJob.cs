using TorreClou.Core.Enums;

namespace TorreClou.Core.Interfaces
{
    /// <summary>
    /// Interface for jobs that can be monitored and recovered by JobHealthMonitor.
    /// Any job entity implementing this interface can be automatically recovered
    /// when it becomes orphaned (stale heartbeat, crashed worker, etc.)
    /// </summary>
    public interface IRecoverableJob
    {
        /// <summary>
        /// Unique identifier for the job.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// Current status of the job (QUEUED, PROCESSING, UPLOADING, etc.)
        /// </summary>
        JobStatus Status { get; set; }

        /// <summary>
        /// Type of job for strategy selection during recovery.
        /// </summary>
        JobType Type { get; }

        /// <summary>
        /// Last heartbeat timestamp from the worker processing this job.
        /// Used to detect stale/orphaned jobs.
        /// </summary>
        DateTime? LastHeartbeat { get; }

        /// <summary>
        /// When the job started processing.
        /// Used as fallback when LastHeartbeat is null.
        /// </summary>
        DateTime? StartedAt { get; }

        /// <summary>
        /// Hangfire job ID for state reconciliation.
        /// </summary>
        string? HangfireJobId { get; set; }

        /// <summary>
        /// Error message from previous failure (cleared on recovery).
        /// </summary>
        string? ErrorMessage { get; set; }
    }
}

