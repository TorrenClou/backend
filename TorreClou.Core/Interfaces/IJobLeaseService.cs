namespace TorreClou.Core.Interfaces
{
    /// <summary>
    /// Service for managing job execution leases to prevent duplicate execution in multi-instance environments.
    /// </summary>
    public interface IJobLeaseService
    {
        /// <summary>
        /// Attempts to atomically acquire a lease on a job.
        /// Only one worker can successfully acquire a lease at a time.
        /// </summary>
        /// <param name="jobId">The job ID to acquire a lease for</param>
        /// <param name="workerId">Unique identifier of the worker attempting to acquire the lease</param>
        /// <param name="leaseDuration">How long the lease should be valid</param>
        /// <returns>Result indicating success or failure with reason</returns>
        Task<LeaseAcquisitionResult> TryAcquireLeaseAsync(int jobId, string workerId, TimeSpan leaseDuration);

        /// <summary>
        /// Refreshes an existing lease, extending its expiration time.
        /// Only succeeds if the caller is the current lease owner.
        /// </summary>
        /// <param name="jobId">The job ID to refresh the lease for</param>
        /// <param name="workerId">Unique identifier of the worker (must match current lease owner)</param>
        /// <param name="leaseDuration">How long to extend the lease</param>
        /// <returns>True if lease was refreshed, false if not owned by this worker or expired</returns>
        Task<bool> RefreshLeaseAsync(int jobId, string workerId, TimeSpan leaseDuration);

        /// <summary>
        /// Releases a lease, making it available for other workers.
        /// Only succeeds if the caller is the current lease owner.
        /// </summary>
        /// <param name="jobId">The job ID to release the lease for</param>
        /// <param name="workerId">Unique identifier of the worker (must match current lease owner)</param>
        Task ReleaseLeaseAsync(int jobId, string workerId);

        /// <summary>
        /// Checks if a job's lease has expired.
        /// </summary>
        /// <param name="jobId">The job ID to check</param>
        /// <returns>True if lease is expired or doesn't exist, false if still valid</returns>
        Task<bool> IsLeaseExpiredAsync(int jobId);
    }

    /// <summary>
    /// Result of a lease acquisition attempt.
    /// </summary>
    public class LeaseAcquisitionResult
    {
        public bool Success { get; set; }
        public LeaseAcquisitionReason Reason { get; set; }

        private LeaseAcquisitionResult(bool success, LeaseAcquisitionReason reason)
        {
            Success = success;
            Reason = reason;
        }

        public static LeaseAcquisitionResult Acquired => new(true, LeaseAcquisitionReason.Acquired);
        public static LeaseAcquisitionResult AlreadyOwned => new(false, LeaseAcquisitionReason.AlreadyOwned);
        public static LeaseAcquisitionResult NotFound => new(false, LeaseAcquisitionReason.NotFound);
        public static LeaseAcquisitionResult LockContention => new(false, LeaseAcquisitionReason.LockContention);
    }

    /// <summary>
    /// Reason for lease acquisition result.
    /// </summary>
    public enum LeaseAcquisitionReason
    {
        Acquired,
        AlreadyOwned,
        NotFound,
        LockContention
    }
}

