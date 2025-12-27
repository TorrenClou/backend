using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.Options;
using TorreClou.Core.Enums;
using TorreClou.Core.Interfaces;
using TorreClou.Core.Interfaces.Hangfire;
using TorreClou.Core.Options;
using TorreClou.Core.Specifications;
using SyncEntity = TorreClou.Core.Entities.Jobs.Sync;

namespace TorreClou.Sync.Worker.Services
{
    /// <summary>
    /// Background service that recovers orphaned or stuck sync jobs.
    /// Runs on startup and periodically.
    /// </summary>
    public class SyncRecoveryService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<SyncRecoveryService> logger,
        IOptions<JobHealthMonitorOptions> options) : BackgroundService
    {
        private readonly JobHealthMonitorOptions _options = options.Value;

        /// <summary>
        /// Maximum number of retries before marking a sync as permanently failed.
        /// </summary>
        private const int MaxRetryCount = 5;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation(
                "[SYNC_RECOVERY] Starting | CheckInterval: {Interval} | StaleThreshold: {Threshold} | MaxRetries: {MaxRetries}",
                _options.CheckInterval, _options.StaleJobThreshold, MaxRetryCount);

            // Initial recovery on startup
            await RecoverStaleSyncsAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_options.CheckInterval, stoppingToken);
                    await RecoverStaleSyncsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Graceful shutdown
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[SYNC_RECOVERY] Loop error");
                }
            }
        }

        private async Task RecoverStaleSyncsAsync(CancellationToken cancellationToken)
        {
            using var scope = serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var backgroundJobClient = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
            var jobStatusService = scope.ServiceProvider.GetRequiredService<IJobStatusService>();
            var monitoringApi = JobStorage.Current?.GetMonitoringApi();

            var now = DateTime.UtcNow;
            var staleCutoff = now - _options.StaleJobThreshold;

            // Query candidates for recovery:
            // 1. SYNC_RETRY: NextRetryAt is null OR due, AND hasn't exceeded max retries
            // 2. SYNCING: stale heartbeat (job died while running)
            // 3. PENDING: stale with no HangfireJobId (never picked up properly)
            var spec = new BaseSpecification<SyncEntity>(s =>
                // SYNC_RETRY jobs that are due for retry
                (s.Status == SyncStatus.SYNC_RETRY &&
                    s.RetryCount < MaxRetryCount &&
                    (s.NextRetryAt == null || s.NextRetryAt <= now)) ||

                // SYNCING jobs with stale heartbeat (worker died)
                (s.Status == SyncStatus.SYNCING &&
                    s.RetryCount < MaxRetryCount &&
                    (
                        (s.LastHeartbeat != null && s.LastHeartbeat < staleCutoff) ||
                        (s.LastHeartbeat == null && s.StartedAt != null && s.StartedAt < staleCutoff)
                    )) ||

                // PENDING jobs that were never picked up (stale StartedAt or due NextRetryAt)
                (s.Status == SyncStatus.PENDING &&
                    s.RetryCount < MaxRetryCount &&
                    string.IsNullOrEmpty(s.HangfireJobId) &&
                    (
                        (s.NextRetryAt != null && s.NextRetryAt <= now) ||
                        (s.StartedAt != null && s.StartedAt < staleCutoff)
                    ))
            );

            var candidates = await unitOfWork.Repository<SyncEntity>().ListAsync(spec);

            if (!candidates.Any())
                return;

            logger.LogWarning("[SYNC_RECOVERY] Found {Count} candidate syncs to inspect", candidates.Count);

            foreach (var sync in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await RecoverSingleSyncAsync(
                        sync, 
                        unitOfWork, 
                        backgroundJobClient, 
                        jobStatusService, 
                        monitoringApi, 
                        now, 
                        staleCutoff);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[SYNC_RECOVERY] Failed to recover SyncId {SyncId}", sync.Id);
                }
            }
        }

        private async Task RecoverSingleSyncAsync(
            SyncEntity sync,
            IUnitOfWork unitOfWork,
            IBackgroundJobClient backgroundJobClient,
            IJobStatusService jobStatusService,
            IMonitoringApi? monitoringApi,
            DateTime now,
            DateTime staleCutoff)
        {
            // Check if we should actually recover this sync
            if (!ShouldRecoverSync(sync, monitoringApi, staleCutoff))
            {
                logger.LogDebug("[SYNC_RECOVERY] Skip SyncId {SyncId} - Hangfire job active/pending", sync.Id);
                return;
            }

            var prevStatus = sync.Status;
            var prevHangfireId = sync.HangfireJobId;

            // Check max retry limit
            if (sync.RetryCount >= MaxRetryCount)
            {
                logger.LogWarning("[SYNC_RECOVERY] SyncId {SyncId} exceeded max retries ({MaxRetries}). Marking as FAILED.",
                    sync.Id, MaxRetryCount);

                sync.CompletedAt = now;
                sync.NextRetryAt = null;

                await jobStatusService.TransitionSyncStatusAsync(
                    sync,
                    SyncStatus.FAILED,
                    StatusChangeSource.Recovery,
                    $"Max retry count ({MaxRetryCount}) exceeded",
                    new { prevStatus, retryCount = sync.RetryCount, recoveryTime = now });

                return;
            }

            // Delete old Hangfire job if it exists to prevent duplicate execution
            if (!string.IsNullOrEmpty(prevHangfireId))
            {
                try
                {
                    backgroundJobClient.Delete(prevHangfireId);
                    logger.LogDebug("[SYNC_RECOVERY] Deleted old Hangfire job {HangfireId} for SyncId {SyncId}",
                        prevHangfireId, sync.Id);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[SYNC_RECOVERY] Failed to delete old Hangfire job {HangfireId}", prevHangfireId);
                    // Continue with recovery anyway
                }
            }

            // Increment retry count and compute next retry time
            sync.RetryCount = Math.Max(0, sync.RetryCount) + 1;
            var nextRetryAt = ComputeNextRetryAt(sync.RetryCount, now);
            sync.NextRetryAt = nextRetryAt;

            // Clear error message for fresh retry
            sync.ErrorMessage = null;

            // Clear old HangfireJobId before creating new one
            sync.HangfireJobId = null;

            // DON'T set LastHeartbeat here - only the running job should update heartbeat
            // This prevents false "alive" signals before the job actually starts

            // Schedule the job at NextRetryAt (respects backoff) instead of immediate enqueue
            string newHangfireId;
            if (nextRetryAt > now)
            {
                // Schedule for future execution
                newHangfireId = backgroundJobClient.Schedule<IS3SyncJob>(
                    x => x.ExecuteAsync(sync.Id, CancellationToken.None),
                    nextRetryAt);

                logger.LogInformation(
                    "[SYNC_RECOVERY] Scheduled SyncId {SyncId} for {ScheduledAt} | RetryCount={Retry}",
                    sync.Id, nextRetryAt, sync.RetryCount);
            }
            else
            {
                // Immediate enqueue (NextRetryAt is in the past or null)
                newHangfireId = backgroundJobClient.Enqueue<IS3SyncJob>(
                    x => x.ExecuteAsync(sync.Id, CancellationToken.None));

                logger.LogInformation(
                    "[SYNC_RECOVERY] Enqueued SyncId {SyncId} immediately | RetryCount={Retry}",
                    sync.Id, sync.RetryCount);
            }

            sync.HangfireJobId = newHangfireId;

            // Transition to SYNC_RETRY with full audit trail via JobStatusService
            await jobStatusService.TransitionSyncStatusAsync(
                sync,
                SyncStatus.SYNC_RETRY,
                StatusChangeSource.Recovery,
                metadata: new
                {
                    prevStatus = prevStatus.ToString(),
                    prevHangfireId,
                    newHangfireId,
                    retryCount = sync.RetryCount,
                    scheduledAt = nextRetryAt,
                    recoveryTime = now
                });

            logger.LogInformation(
                "[SYNC_RECOVERY] Recovered SyncId {SyncId} | {PrevStatus} -> SYNC_RETRY | PrevHF={PrevHF} -> NewHF={NewHF} | RetryCount={Retry}",
                sync.Id, prevStatus, prevHangfireId, newHangfireId, sync.RetryCount);
        }

        private bool ShouldRecoverSync(SyncEntity sync, IMonitoringApi? monitoringApi, DateTime staleCutoff)
        {
            // If no HangfireJobId, definitely recover
            if (string.IsNullOrWhiteSpace(sync.HangfireJobId))
                return true;

            // If no monitoring API available, recover (DB is source of truth)
            if (monitoringApi == null)
                return true;

            try
            {
                var jobDetails = monitoringApi.JobDetails(sync.HangfireJobId);

                // Job not found in Hangfire - recover
                if (jobDetails == null)
                {
                    logger.LogWarning("[SYNC_RECOVERY] Hangfire job not found | SyncId={SyncId} | HF={HF}",
                        sync.Id, sync.HangfireJobId);
                    return true;
                }

                // Get the current state (FIRST is most recent in Hangfire history)
                var currentState = jobDetails.History?.FirstOrDefault()?.StateName;

                if (string.IsNullOrWhiteSpace(currentState))
                    return true;

                // If HF job is queued or scheduled, don't duplicate
                if (currentState is "Enqueued" or "Scheduled")
                {
                    logger.LogDebug("[SYNC_RECOVERY] SyncId {SyncId} is {State} in Hangfire, skipping recovery",
                        sync.Id, currentState);
                    return false;
                }

                // If HF job is processing, only recover if DB heartbeat is stale
                if (currentState == "Processing")
                {
                    var dbStale =
                        (sync.LastHeartbeat != null && sync.LastHeartbeat < staleCutoff) ||
                        (sync.LastHeartbeat == null && sync.StartedAt != null && sync.StartedAt < staleCutoff);

                    if (dbStale)
                    {
                        logger.LogWarning(
                            "[SYNC_RECOVERY] HF=Processing but DB stale => recover | SyncId={SyncId} | HF={HF} | LastHeartbeat={LH}",
                            sync.Id, sync.HangfireJobId, sync.LastHeartbeat);
                        return true;
                    }

                    // Job is actively processing and heartbeat is fresh - don't recover
                    return false;
                }

                // If failed or deleted in Hangfire - recover
                if (currentState is "Failed" or "Deleted")
                {
                    logger.LogWarning("[SYNC_RECOVERY] HF={State} => recover | SyncId={SyncId}",
                        currentState, sync.Id);
                    return true;
                }

                // If succeeded in Hangfire but DB shows not completed - state mismatch, recover
                if (currentState == "Succeeded" && sync.Status != SyncStatus.COMPLETED)
                {
                    logger.LogWarning(
                        "[SYNC_RECOVERY] HF=Succeeded but DB={Status} => recover | SyncId={SyncId}",
                        sync.Status, sync.Id);
                    return true;
                }

                // For any other unknown states, default to recover
                return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[SYNC_RECOVERY] Hangfire state check failed | SyncId={SyncId}", sync.Id);
                // On error, default to recover
                return true;
            }
        }

        private static DateTime ComputeNextRetryAt(int retryCount, DateTime nowUtc)
        {
            // Exponential backoff with cap at 30 minutes:
            // Retry 1 => 30s, Retry 2 => 60s, Retry 3 => 120s, Retry 4 => 240s, Retry 5 => 480s...
            var seconds = Math.Min(1800, 30 * (int)Math.Pow(2, Math.Min(10, retryCount - 1)));
            return nowUtc.AddSeconds(seconds);
        }
    }
}
