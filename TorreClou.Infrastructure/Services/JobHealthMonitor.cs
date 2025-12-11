using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TorreClou.Core.Entities;
using TorreClou.Core.Enums;
using TorreClou.Core.Interfaces;
using TorreClou.Core.Options;
using TorreClou.Core.Specifications;

namespace TorreClou.Infrastructure.Services
{
    /// <summary>
    /// Generic background service that monitors job health and recovers orphaned jobs.
    /// Works with any entity implementing IRecoverableJob that extends BaseEntity.
    /// Runs periodically to detect jobs that:
    /// - Are stuck in PROCESSING/UPLOADING with stale heartbeat
    /// - Have Hangfire jobs that failed/succeeded but DB wasn't updated
    /// Uses strategy pattern for job-type-specific recovery logic.
    /// </summary>
    /// <typeparam name="TJob">The job entity type extending BaseEntity and implementing IRecoverableJob</typeparam>
    public class JobHealthMonitor<TJob> : BackgroundService
        where TJob : BaseEntity, IRecoverableJob
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<JobHealthMonitor<TJob>> _logger;
        private readonly Dictionary<JobType, IJobRecoveryStrategy> _strategies;
        private readonly JobHealthMonitorOptions _options;

        public JobHealthMonitor(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<JobHealthMonitor<TJob>> logger,
            IEnumerable<IJobRecoveryStrategy> strategies,
            IOptions<JobHealthMonitorOptions> options)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            _strategies = strategies.ToDictionary(s => s.SupportedJobType);
            _options = options.Value;

            _logger.LogInformation(
                "[HEALTH] JobHealthMonitor<{JobType}> initialized | Strategies: {Strategies} | CheckInterval: {CheckInterval} | StaleThreshold: {StaleThreshold}",
                typeof(TJob).Name,
                string.Join(", ", _strategies.Keys),
                _options.CheckInterval,
                _options.StaleJobThreshold);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[HEALTH] Job health monitor started for {JobType}", typeof(TJob).Name);

            // Initial recovery on startup
            await RecoverOrphanedJobsAsync(stoppingToken);

            // Continuous monitoring loop
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_options.CheckInterval, stoppingToken);
                    await RecoverOrphanedJobsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[HEALTH] Error during health check. Will retry in {Interval}", _options.CheckInterval);
                }
            }

            _logger.LogInformation("[HEALTH] Job health monitor stopped for {JobType}", typeof(TJob).Name);
        }

        private async Task RecoverOrphanedJobsAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var backgroundJobClient = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
            var monitoringApi = JobStorage.Current?.GetMonitoringApi();

            var staleTime = DateTime.UtcNow - _options.StaleJobThreshold;

            // Find jobs stuck in PROCESSING or UPLOADING with stale heartbeat
            var stuckJobsSpec = new BaseSpecification<TJob>(j =>
                (j.Status == JobStatus.PROCESSING || j.Status == JobStatus.UPLOADING) &&
                (
                    (j.LastHeartbeat != null && j.LastHeartbeat < staleTime) ||
                    (j.LastHeartbeat == null && j.StartedAt != null && j.StartedAt < staleTime)
                ));

            var stuckJobs = await unitOfWork.Repository<TJob>().ListAsync(stuckJobsSpec);

            if (!stuckJobs.Any())
            {
                _logger.LogDebug("[HEALTH] No orphaned jobs found");
                return;
            }

            _logger.LogWarning("[HEALTH] Found {Count} potentially orphaned jobs", stuckJobs.Count);

            foreach (var job in stuckJobs)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    // Check Hangfire state if we have the job ID
                    var shouldRecover = ShouldRecoverJob(job, monitoringApi);

                    if (!shouldRecover)
                    {
                        _logger.LogDebug("[HEALTH] Job still processing in Hangfire | JobId: {JobId}", job.Id);
                        continue;
                    }

                    await RecoverJobAsync(job, unitOfWork, backgroundJobClient);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[HEALTH] Failed to recover job | JobId: {JobId}", job.Id);
                }
            }
        }

        private bool ShouldRecoverJob(IRecoverableJob job, IMonitoringApi? monitoringApi)
        {
            // If we don't have a Hangfire job ID, assume we should recover
            if (string.IsNullOrEmpty(job.HangfireJobId) || monitoringApi == null)
            {
                return true;
            }

            try
            {
                var hangfireJob = monitoringApi.JobDetails(job.HangfireJobId);

                if (hangfireJob == null)
                {
                    _logger.LogWarning("[HEALTH] Hangfire job not found | JobId: {JobId} | HangfireJobId: {HangfireJobId}",
                        job.Id, job.HangfireJobId);
                    return true;
                }

                var currentState = hangfireJob.History.FirstOrDefault()?.StateName;

                // If job is still "Processing" in Hangfire but our DB heartbeat is stale,
                // Hangfire hasn't detected the dead worker yet - force recovery
                if (currentState == "Processing")
                {
                    _logger.LogWarning(
                        "[HEALTH] Hangfire shows Processing but heartbeat is stale - forcing recovery | JobId: {JobId} | HangfireJobId: {HangfireJobId}",
                        job.Id, job.HangfireJobId);
                    return true;
                }

                // Enqueued/Scheduled means job is pending in Hangfire - don't duplicate
                if (currentState == "Enqueued" || currentState == "Scheduled")
                {
                    return false;
                }

                // If Hangfire shows succeeded but DB shows processing - need to sync
                if (currentState == "Succeeded" && job.Status == JobStatus.PROCESSING)
                {
                    _logger.LogWarning("[HEALTH] Hangfire succeeded but DB shows processing | JobId: {JobId}", job.Id);
                    return true;
                }

                // If Hangfire shows failed, we should recover (re-enqueue)
                if (currentState == "Failed" || currentState == "Deleted")
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[HEALTH] Failed to check Hangfire state | JobId: {JobId}", job.Id);
            }

            return true;
        }

        private async Task RecoverJobAsync(IRecoverableJob job, IUnitOfWork unitOfWork, IBackgroundJobClient backgroundJobClient)
        {
            // Find the appropriate strategy for this job type
            if (!_strategies.TryGetValue(job.Type, out var strategy))
            {
                _logger.LogWarning(
                    "[HEALTH] No recovery strategy registered for job type | JobId: {JobId} | Type: {Type}",
                    job.Id, job.Type);
                return;
            }

            _logger.LogInformation(
                "[HEALTH] Recovering orphaned job | JobId: {JobId} | Type: {Type} | Status: {Status} | LastHeartbeat: {LastHeartbeat}",
                job.Id, job.Type, job.Status, job.LastHeartbeat);

            // Delegate recovery to the strategy
            var hangfireJobId = strategy.RecoverJob(job, backgroundJobClient);

            // Update job with new Hangfire job ID
            job.HangfireJobId = hangfireJobId;
            job.ErrorMessage = null; // Clear previous error
            await unitOfWork.Complete();

            _logger.LogInformation(
                "[HEALTH] Job recovered and re-enqueued | JobId: {JobId} | HangfireJobId: {HangfireJobId}",
                job.Id, hangfireJobId);
        }
    }
}

