using TorreClou.Core.Entities.Jobs;
using TorreClou.Core.Enums;
using TorreClou.Core.Interfaces;
using TorreClou.Core.Specifications;
using TorreClou.Infrastructure.Tracing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TorreClou.Infrastructure.Settings;

namespace TorreClou.Infrastructure.Workers
{
    /// <summary>
    /// Abstract base class for Hangfire jobs that process UserJob entities.
    /// Implements Template Method pattern for consistent job lifecycle management.
    /// </summary>
    /// <typeparam name="TJob">The concrete job type for logger categorization.</typeparam>
    public abstract class BaseJob<TJob> where TJob : class
    {
        protected readonly IUnitOfWork UnitOfWork;
        protected readonly ILogger<TJob> Logger;
        protected readonly IJobLeaseService LeaseService;
        protected readonly JobLeaseSettings LeaseSettings;
        private readonly string _workerId;

        /// <summary>
        /// Log prefix for consistent logging (e.g., "[DOWNLOAD]", "[UPLOAD]").
        /// </summary>
        protected abstract string LogPrefix { get; }

        protected BaseJob(
            IUnitOfWork unitOfWork, 
            ILogger<TJob> logger,
            IJobLeaseService leaseService,
            IOptions<JobLeaseSettings> leaseSettings)
        {
            UnitOfWork = unitOfWork;
            Logger = logger;
            LeaseService = leaseService;
            LeaseSettings = leaseSettings.Value;
            _workerId = GenerateWorkerId();
        }

        /// <summary>
        /// Generates a unique worker identifier for lease ownership.
        /// Format: MachineName-ProcessId-ThreadId
        /// </summary>
        private static string GenerateWorkerId()
        {
            return $"{Environment.MachineName}-{Environment.ProcessId}-{Thread.CurrentThread.ManagedThreadId}";
        }

        /// <summary>
        /// Template method that orchestrates the job execution lifecycle.
        /// Subclasses should override ExecuteCoreAsync for specific job logic.
        /// Wrapped in Datadog span for distributed tracing.
        /// </summary>
        public async Task ExecuteAsync(int jobId, CancellationToken cancellationToken = default)
        {
            var operationName = $"job.{LogPrefix.Trim('[', ']').ToLowerInvariant()}.execute";
            
            using var span = Tracing.Tracing.StartSpan(operationName, $"Job {jobId}")
                .WithTag("job.id", jobId)
                .WithTag("job.type", GetType().Name)
                .WithTag("job.prefix", LogPrefix);

            Logger.LogInformation("{LogPrefix} Starting job | JobId: {JobId}", LogPrefix, jobId);

            UserJob? job = null;

            try
            {
                // 1. Load job from database
                using (Tracing.Tracing.StartChildSpan("job.load"))
                {
                    job = await LoadJobAsync(jobId);
                }
                
                if (job == null)
                {
                    span.WithTag("job.status", "not_found").AsError();
                    return;
                }

                // Add job details to span
                span.WithTag("job.user_id", job.UserId)
                    .WithTag("job.status.initial", job.Status.ToString());

                // 2. Check if job is FAILED - never reprocess failed jobs
                if (job.Status == JobStatus.FAILED)
                {
                    Logger.LogWarning("{LogPrefix} Job is FAILED and will not be reprocessed | JobId: {JobId}", 
                        LogPrefix, jobId);
                    span.WithTag("job.status", "failed_skipped")
                        .WithTag("job.status.final", job.Status.ToString());
                    return;
                }

                // 3. Check if already completed or cancelled
                if (IsJobTerminated(job))
                {
                    Logger.LogInformation("{LogPrefix} Job already finished | JobId: {JobId} | Status: {Status}", 
                        LogPrefix, jobId, job.Status);
                    span.WithTag("job.status", "already_terminated")
                        .WithTag("job.status.final", job.Status.ToString());
                    return;
                }

                // 4. Atomically acquire lease to prevent duplicate execution
                var leaseDuration = TimeSpan.FromMinutes(LeaseSettings.LeaseDurationMinutes);
                var leaseResult = await LeaseService.TryAcquireLeaseAsync(jobId, _workerId, leaseDuration);
                
                if (!leaseResult.Success)
                {
                    Logger.LogWarning(
                        "{LogPrefix} Failed to acquire lease | JobId: {JobId} | Reason: {Reason} | WorkerId: {Worker}",
                        LogPrefix, jobId, leaseResult.Reason, _workerId);
                    span.WithTag("job.status", "lease_acquisition_failed")
                        .WithTag("job.lease_reason", leaseResult.Reason.ToString())
                        .WithTag("job.status.final", job.Status.ToString());
                    return;
                }

                Logger.LogInformation(
                    "{LogPrefix} Lease acquired | JobId: {JobId} | WorkerId: {Worker} | LeaseDuration: {Duration}min",
                    LogPrefix, jobId, _workerId, LeaseSettings.LeaseDurationMinutes);

                // 5. Execute the core job logic
                using (Tracing.Tracing.StartChildSpan("job.execute_core"))
                {
                    await ExecuteCoreAsync(job, cancellationToken);
                }
                
                // Release lease on successful completion
                await LeaseService.ReleaseLeaseAsync(job.Id, _workerId);
                
                span.WithTag("job.status", "success")
                    .WithTag("job.status.final", job.Status.ToString());
            }
            catch (OperationCanceledException)
            {
                Logger.LogWarning("{LogPrefix} Job cancelled | JobId: {JobId}", LogPrefix, jobId);
                span.WithTag("job.status", "cancelled").AsError();
                
                if (job != null)
                {
                    span.WithTag("job.status.final", job.Status.ToString());
                    await OnJobCancelledAsync(job);
                    // Release lease on cancellation
                    await LeaseService.ReleaseLeaseAsync(job.Id, _workerId);
                }
                throw; // Let Hangfire handle the cancellation
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "{LogPrefix} Fatal error | JobId: {JobId}", LogPrefix, jobId);
                
                span.WithTag("job.status", "error").WithException(ex);

                if (job != null)
                {
                    // Check if retries are available before marking as failed
                    bool hasRetries = HasRetriesAvailable(job);
                    var finalStatus = hasRetries ? JobStatus.RETRYING : JobStatus.FAILED;
                    
                    span.WithTag("job.status.final", finalStatus.ToString())
                        .WithTag("job.has_retries", hasRetries.ToString());
                    
                    await OnJobErrorAsync(job, ex);
                    await MarkJobFailedAsync(job, ex.Message, hasRetries);
                    
                    // Release lease on error (will be reacquired on retry if needed)
                    await LeaseService.ReleaseLeaseAsync(job.Id, _workerId);
                }

                throw; // Let Hangfire retry if attempts remain
            }
        }

        /// <summary>
        /// Core job execution logic. Override in derived classes.
        /// </summary>
        protected abstract Task ExecuteCoreAsync(UserJob job, CancellationToken cancellationToken);

        /// <summary>
        /// Configure the specification with job-specific includes.
        /// Override in derived classes to add related entities.
        /// </summary>
        protected abstract void ConfigureSpecification(BaseSpecification<UserJob> spec);

        /// <summary>
        /// Hook for cleanup when job is cancelled. Override for specific cleanup logic.
        /// </summary>
        protected virtual Task OnJobCancelledAsync(UserJob job)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Hook for cleanup when job encounters an error. Override for specific cleanup logic.
        /// </summary>
        protected virtual Task OnJobErrorAsync(UserJob job, Exception exception)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Loads the job from the database with configured includes.
        /// </summary>
        protected async Task<UserJob?> LoadJobAsync(int jobId)
        {
            var spec = new BaseSpecification<UserJob>(j => j.Id == jobId);
            ConfigureSpecification(spec);

            var job = await UnitOfWork.Repository<UserJob>().GetEntityWithSpec(spec);

            if (job == null)
            {
                Logger.LogError("{LogPrefix} Job not found | JobId: {JobId}", LogPrefix, jobId);
            }

            return job;
        }

        /// <summary>
        /// Checks if the job is in a terminal state (COMPLETED or CANCELLED).
        /// Note: FAILED is checked separately and never reprocessed.
        /// </summary>
        protected bool IsJobTerminated(UserJob job)
        {
            return job.Status == JobStatus.COMPLETED || job.Status == JobStatus.CANCELLED;
        }


        /// <summary>
        /// Updates the job's heartbeat and current state, and refreshes the lease.
        /// </summary>
        protected async Task UpdateHeartbeatAsync(UserJob job, string? state = null)
        {
            try
            {
                job.LastHeartbeat = DateTime.UtcNow;
                if (state != null)
                {
                    job.CurrentState = state;
                }
                
                // Refresh the lease to extend execution time
                var leaseDuration = TimeSpan.FromMinutes(LeaseSettings.LeaseDurationMinutes);
                var refreshed = await LeaseService.RefreshLeaseAsync(job.Id, _workerId, leaseDuration);
                
                if (!refreshed)
                {
                    Logger.LogWarning(
                        "{LogPrefix} Failed to refresh lease during heartbeat | JobId: {JobId} | WorkerId: {Worker}",
                        LogPrefix, job.Id, _workerId);
                }
                
                await UnitOfWork.Complete();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "{LogPrefix} Failed to update heartbeat | JobId: {JobId}", LogPrefix, job.Id);
            }
        }

        /// <summary>
        /// Marks the job as failed with an error message.
        /// If retries are available (determined by Hangfire), sets status to RETRYING instead of FAILED.
        /// </summary>
        protected async Task MarkJobFailedAsync(UserJob job, string errorMessage, bool hasRetries = false)
        {
            try
            {
                if (hasRetries)
                {
                    // Job will be retried by Hangfire - mark as RETRYING
                    job.Status = JobStatus.RETRYING;
                    job.ErrorMessage = errorMessage;
                    // NextRetryAt will be set by Hangfire's retry mechanism
                    // We estimate it based on typical retry delays: 60s, 300s, 900s
                    // This is approximate - Hangfire will handle actual scheduling
                    job.NextRetryAt = DateTime.UtcNow.AddMinutes(1); // Conservative estimate
                    
                    Logger.LogWarning("{LogPrefix} Job marked as RETRYING | JobId: {JobId} | Error: {Error} | NextRetryAt: {NextRetry}", 
                        LogPrefix, job.Id, errorMessage, job.NextRetryAt);
                }
                else
                {
                    // All retries exhausted - mark as FAILED
                    job.Status = JobStatus.FAILED;
                    job.ErrorMessage = errorMessage;
                    job.CompletedAt = DateTime.UtcNow;
                    job.NextRetryAt = null; // Clear retry time
                    
                    Logger.LogError("{LogPrefix} Job marked as FAILED (no retries remaining) | JobId: {JobId} | Error: {Error}", 
                        LogPrefix, job.Id, errorMessage);
                }
                
                await UnitOfWork.Complete();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "{LogPrefix} Failed to mark job status | JobId: {JobId}", LogPrefix, job.Id);
            }
        }

        /// <summary>
        /// Determines if Hangfire retries are likely available for this job.
        /// This is a heuristic - Hangfire's AutomaticRetry attribute controls actual retries.
        /// </summary>
        protected bool HasRetriesAvailable(UserJob job)
        {
            // If job is already FAILED, no retries
            if (job.Status == JobStatus.FAILED)
                return false;

            // If job is in RETRYING state, assume retries might still be available
            // (Hangfire will eventually mark as FAILED when exhausted)
            if (job.Status == JobStatus.RETRYING)
                return true;

            // For other states, assume retries might be available
            // Hangfire's AutomaticRetry will handle actual retry logic
            return true;
        }
    }
}

