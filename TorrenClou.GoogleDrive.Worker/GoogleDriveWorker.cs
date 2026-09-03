using StackExchange.Redis;
using TorrenClou.Core.Entities.Jobs;
using TorrenClou.Core.Enums;
using TorrenClou.Core.Interfaces;
using TorrenClou.Infrastructure.Workers;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using TorrenClou.Core.Interfaces.Hangfire;

namespace TorrenClou.GoogleDrive.Worker
{
    public class GoogleDriveWorker(
        ILogger<GoogleDriveWorker> logger,
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopeFactory) : BaseStreamWorker(logger, redis, scopeFactory)
    {
        protected override string StreamKey => "uploads:googledrive:stream";
        protected override string ConsumerGroupName => "googledrive-workers";

        protected override async Task<bool> ProcessJobAsync(StreamEntry entry, IServiceProvider services, CancellationToken token)
        {
            // 1. Use Base Helper for Safe Parsing
            var jobId = ParseJobId(entry);
            if (!jobId.HasValue)
            {
                Logger.LogWarning("[GD_WORKER] Invalid/Missing JobId. Acking to remove.");
                return true;
            }

            // 2. Resolve Services (Scoped is handled by Base Class) (GET USER SERVICES DIRECTLY INSTEAD OF UOW)
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();
            var backgroundJobClient = services.GetRequiredService<IBackgroundJobClient>();

            // 3. Load Job & Idempotency Check
            var job = await unitOfWork.Repository<UserJob>().GetByIdAsync(jobId.Value);

            if (job == null)
            {
                Logger.LogError("[GD_WORKER] Job {Id} not found in DB. Acking.", jobId);
                return true;
            }

            // CRITICAL: Prevent duplicate uploads if worker restarts before ACK
            if (!string.IsNullOrEmpty(job.HangfireUploadJobId))
            {
                Logger.LogInformation("[GD_WORKER] Job {Id} already enqueued (HF: {HfId}). Skipping.",
                    jobId, job.HangfireJobId);
                return true;
            }

            // 4. Route before enqueueing.
            // The upload job re-checks this, but resolving here means a job whose drive died
            // during the download is moved to a healthy account before it ever reaches the
            // queue — and one with nowhere to go fails immediately instead of burning
            // Hangfire retries.
            var routingService = services.GetRequiredService<IUploadRoutingService>();
            var route = await routingService.ResolveTargetAsync(job, job.TotalBytes, token);

            if (!route.HasTarget)
            {
                var jobStatusService = services.GetRequiredService<IJobStatusService>();
                var message = route.Message ?? "No healthy storage destination is available for this upload.";

                Logger.LogError("[GD_WORKER] No healthy destination for Job {Id}. {Message}", jobId, message);

                job.CompletedAt = DateTime.UtcNow;
                await jobStatusService.TransitionJobStatusAsync(
                    job,
                    JobStatus.UPLOAD_FAILED,
                    StatusChangeSource.System,
                    message,
                    new { storageRoutingFailed = true, profileId = job.StorageProfileId });

                return true; // Acked: requeuing cannot help until the user reconnects a drive.
            }

            if (route.Rerouted)
            {
                Logger.LogWarning("[GD_WORKER] Job {Id} rerouted before dispatch | {Message}", jobId, route.Message);
            }

            // 5. Enqueue to Hangfire
            // We don't need to pass downloadPath/profileId manually;
            // the Job itself (GoogleDriveUploadJob) should load the entity from DB to get those details.
            Logger.LogInformation("[GD_WORKER] Enqueuing Job {Id}...", jobId);

            var hangfireJobId = backgroundJobClient.Enqueue<IGoogleDriveUploadJob>(
                service => service.ExecuteAsync(jobId.Value, CancellationToken.None));

            // 6. Update State
            job.HangfireJobId = hangfireJobId;
            job.Status = JobStatus.PENDING_UPLOAD;
            job.CurrentState = "Queued for Google Drive Upload";
            job.LastHeartbeat = DateTime.UtcNow;

            await unitOfWork.Complete();

            Logger.LogInformation("[GD_WORKER] Success | JobId: {JobId} -> HF: {HfId}", jobId, hangfireJobId);

            return true; // Base class handles the XACK
        }
    }
}