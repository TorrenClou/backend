using Microsoft.Extensions.Logging;
using TorreClou.Core.Entities.Jobs;
using TorreClou.Core.Enums;
using TorreClou.Core.Interfaces;
using TorreClou.Core.Specifications;
using SyncEntity = TorreClou.Core.Entities.Jobs.Sync;

namespace TorreClou.Infrastructure.Workers
{
    /// <summary>
    /// Abstract base class for Hangfire jobs that process Sync entities.
    /// Implements Template Method pattern for consistent sync job lifecycle management.
    /// </summary>
    /// <typeparam name="TJob">The concrete job type for logger categorization.</typeparam>
    public abstract class SyncJobBase<TJob>(
        IUnitOfWork unitOfWork,
        ILogger<TJob> logger,
        IJobStatusService jobStatusService) : JobBase<TJob>(unitOfWork, logger) where TJob : class
    {
        protected IJobStatusService JobStatusService { get; } = jobStatusService;

        /// <summary>
        /// Maximum number of retries before marking a sync as permanently failed.
        /// Should match SyncRecoveryService.MaxRetryCount.
        /// </summary>
        protected const int MaxRetryCount = 5;

        /// <summary>
        /// Template method that orchestrates the sync job execution lifecycle.
        /// Subclasses should override ExecuteCoreAsync for specific sync logic.
        /// </summary>
        public async Task ExecuteAsync(int syncId, CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("{LogPrefix} Starting sync job | SyncId: {SyncId}", LogPrefix, syncId);
            SyncEntity? sync = null;
            UserJob? job = null;

            try
            {
                // 1. Load sync entity from database
                sync = await LoadSyncAsync(syncId);
                if (sync == null)
                {
                    return;
                }

                // 2. Load related UserJob
                job = await LoadUserJobAsync(sync.JobId);
                if (job == null)
                {
                    await JobStatusService.TransitionSyncStatusAsync(
                        sync,
                        SyncStatus.FAILED,
                        StatusChangeSource.Worker,
                        "UserJob not found");
                    return;
                }

                // 3. Check if sync is in a terminal state
                if (IsSyncTerminated(sync))
                {
                    Logger.LogInformation("{LogPrefix} Sync already finished | SyncId: {SyncId} | Status: {Status}",
                        LogPrefix, syncId, sync.Status);
                    return;
                }

                // 4. Set initial heartbeat to signal job has started
                // This happens BEFORE ExecuteCoreAsync so recovery can detect active jobs
                await SetInitialHeartbeatAsync(sync);

                // 5. Execute the core sync logic
                await ExecuteCoreAsync(sync, job, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Logger.LogError("{LogPrefix} Sync cancelled | SyncId: {SyncId}", LogPrefix, syncId);
                if (sync != null)
                    await OnSyncCancelledAsync(sync);

                throw; // Let Hangfire handle the cancellation
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "{LogPrefix} Fatal error | SyncId: {SyncId}", LogPrefix, syncId);

                if (sync != null)
                {
                    await OnSyncErrorAsync(sync, ex);
                    await MarkSyncFailedAsync(sync, ex.Message, hasRetries: true);
                }

                throw; // Let Hangfire retry if attempts remain
            }
        }

        /// <summary>
        /// Core sync execution logic. Override in derived classes.
        /// </summary>
        protected abstract Task ExecuteCoreAsync(SyncEntity sync, UserJob job, CancellationToken cancellationToken);

        /// <summary>
        /// Sets initial heartbeat and started timestamp when job begins processing.
        /// This helps recovery service detect jobs that have actually started.
        /// </summary>
        protected virtual async Task SetInitialHeartbeatAsync(SyncEntity sync)
        {
            try
            {
                var now = DateTime.UtcNow;
                sync.StartedAt ??= now;
                sync.LastHeartbeat = now;
                await UnitOfWork.Complete();

                Logger.LogDebug("{LogPrefix} Set initial heartbeat | SyncId: {SyncId} | Heartbeat: {Heartbeat}",
                    LogPrefix, sync.Id, now);
            }
            catch (Exception ex)
            {
                // Non-fatal - log warning and continue
                Logger.LogWarning(ex, "{LogPrefix} Failed to set initial heartbeat | SyncId: {SyncId}",
                    LogPrefix, sync.Id);
            }
        }

        /// <summary>
        /// Configure the specification with sync-specific includes.
        /// Override in derived classes to add related entities.
        /// </summary>
        protected virtual void ConfigureSyncSpecification(BaseSpecification<SyncEntity> spec)
        {
            spec.AddInclude(s => s.UserJob);
        }

        /// <summary>
        /// Configure the specification for loading the related UserJob.
        /// Override in derived classes to add related entities.
        /// </summary>
        protected virtual void ConfigureUserJobSpecification(BaseSpecification<UserJob> spec)
        {
            spec.AddInclude(j => j.StorageProfile);
            spec.AddInclude(j => j.User);
        }

        /// <summary>
        /// Hook for cleanup when sync is cancelled. Override for specific cleanup logic.
        /// </summary>
        protected virtual Task OnSyncCancelledAsync(SyncEntity sync)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Hook for cleanup when sync encounters an error. Override for specific cleanup logic.
        /// </summary>
        protected virtual Task OnSyncErrorAsync(SyncEntity sync, Exception exception)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Loads the sync entity from the database with configured includes.
        /// </summary>
        protected async Task<SyncEntity?> LoadSyncAsync(int syncId)
        {
            var spec = new BaseSpecification<SyncEntity>(s => s.Id == syncId);
            ConfigureSyncSpecification(spec);

            var sync = await UnitOfWork.Repository<SyncEntity>().GetEntityWithSpec(spec);

            if (sync == null)
            {
                Logger.LogError("{LogPrefix} Sync not found | SyncId: {SyncId}", LogPrefix, syncId);
            }

            return sync;
        }

        /// <summary>
        /// Loads the related UserJob from the database.
        /// </summary>
        protected async Task<UserJob?> LoadUserJobAsync(int jobId)
        {
            var spec = new BaseSpecification<UserJob>(j => j.Id == jobId);
            ConfigureUserJobSpecification(spec);

            var job = await UnitOfWork.Repository<UserJob>().GetEntityWithSpec(spec);

            if (job == null)
            {
                Logger.LogError("{LogPrefix} UserJob not found | JobId: {JobId}", LogPrefix, jobId);
            }

            return job;
        }

        /// <summary>
        /// Checks if the sync is in a terminal state (COMPLETED or FAILED).
        /// </summary>
        protected bool IsSyncTerminated(SyncEntity sync)
        {
            return sync.Status == SyncStatus.COMPLETED || sync.Status == SyncStatus.FAILED;
        }

        /// <summary>
        /// Marks the sync as failed or retry with an error message.
        /// Uses MaxRetryCount constant for consistency with SyncRecoveryService.
        /// </summary>
        protected async Task MarkSyncFailedAsync(SyncEntity sync, string errorMessage, bool hasRetries = false)
        {
            try
            {
                if (hasRetries && sync.RetryCount < MaxRetryCount)
                {
                    sync.RetryCount++;
                    // Exponential backoff: 1 min, 2 min, 4 min, 8 min, 16 min
                    var delayMinutes = (int)Math.Pow(2, Math.Min(sync.RetryCount - 1, 4));
                    sync.NextRetryAt = DateTime.UtcNow.AddMinutes(delayMinutes);

                    await JobStatusService.TransitionSyncStatusAsync(
                        sync,
                        SyncStatus.SYNC_RETRY,
                        StatusChangeSource.Worker,
                        errorMessage,
                        new { retryCount = sync.RetryCount, nextRetryAt = sync.NextRetryAt, maxRetries = MaxRetryCount });

                    Logger.LogWarning("{LogPrefix} Sync marked as SYNC_RETRY | SyncId: {SyncId} | Error: {Error} | RetryCount: {RetryCount}/{MaxRetries} | NextRetryAt: {NextRetry}",
                        LogPrefix, sync.Id, errorMessage, sync.RetryCount, MaxRetryCount, sync.NextRetryAt);
                }
                else
                {
                    sync.CompletedAt = DateTime.UtcNow;
                    sync.NextRetryAt = null;

                    await JobStatusService.TransitionSyncStatusAsync(
                        sync,
                        SyncStatus.FAILED,
                        StatusChangeSource.Worker,
                        errorMessage,
                        new { retryCount = sync.RetryCount, completedAt = sync.CompletedAt, exhaustedRetries = hasRetries });

                    Logger.LogError("{LogPrefix} Sync marked as FAILED (no retries remaining) | SyncId: {SyncId} | Error: {Error} | RetryCount: {RetryCount}/{MaxRetries}",
                        LogPrefix, sync.Id, errorMessage, sync.RetryCount, MaxRetryCount);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "{LogPrefix} Failed to mark sync status | SyncId: {SyncId}", LogPrefix, sync.Id);
            }
        }

        /// <summary>
        /// Updates the sync's progress.
        /// </summary>
        protected async Task UpdateSyncProgressAsync(SyncEntity sync, long bytesSynced, int filesSynced)
        {
            try
            {
                sync.BytesSynced = bytesSynced;
                sync.FilesSynced = filesSynced;
                sync.LastHeartbeat = DateTime.UtcNow; // Update heartbeat with progress
                await UnitOfWork.Complete();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "{LogPrefix} Failed to update sync progress | SyncId: {SyncId}", LogPrefix, sync.Id);
            }
        }
    }
}
