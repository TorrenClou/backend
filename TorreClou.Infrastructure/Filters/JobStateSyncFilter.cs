using Amazon.Runtime.Internal.Util;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TorreClou.Core.Entities.Jobs;
using TorreClou.Core.Enums;
using TorreClou.Core.Interfaces;

namespace TorreClou.Infrastructure.Filters
{
    public class JobStateSyncFilter(IServiceScopeFactory scopeFactory, ILogger<JobStateSyncFilter> logger) : IElectStateFilter
    {
        public void OnStateElection(ElectStateContext context)
        {
            // Only care if the job is moving to the FAILED state (e.g., retries exhausted)
            if (context.CandidateState is FailedState failedState)
            {
                var jobId = context.GetJobParameter<int>("JobId");
                if (jobId == 0 && context.BackgroundJob.Job.Args.Count > 0 && context.BackgroundJob.Job.Args[0] is int id)
                {
                    jobId = id;
                }

                if (jobId > 0)
                {
                    UpdateJobStatusToFailed(jobId, failedState.Exception.Message).Wait();
                }
            }
        }

        private async Task UpdateJobStatusToFailed(int jobId, string error)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var job = await unitOfWork.Repository<UserJob>().GetByIdAsync(jobId);

                if (job != null && 
                    job.Status != JobStatus.FAILED && 
                    job.Status != JobStatus.TORRENT_FAILED &&
                    job.Status != JobStatus.UPLOAD_FAILED &&
                    job.Status != JobStatus.GOOGLE_DRIVE_FAILED)
                {
                    // Determine appropriate failure state based on current status
                    JobStatus failureStatus = job.Status switch
                    {
                        JobStatus.QUEUED or JobStatus.DOWNLOADING or JobStatus.TORRENT_DOWNLOAD_RETRY or JobStatus.TORRENT_FAILED => JobStatus.TORRENT_FAILED,
                        JobStatus.SYNCING or JobStatus.SYNC_RETRY => JobStatus.UPLOAD_FAILED,
                        JobStatus.UPLOADING or JobStatus.UPLOAD_RETRY => JobStatus.UPLOAD_FAILED,
                        _ => JobStatus.FAILED // Generic failure for unknown states
                    };
                    
                    logger.LogError("[Filter] Marking job {JobId} as {FailureStatus} due to Hangfire failure (all retries exhausted).", jobId, failureStatus);
                    job.Status = failureStatus;
                    job.ErrorMessage = $"System Failure: {error}";
                    job.CompletedAt = DateTime.UtcNow;
                    job.NextRetryAt = null; // Clear retry time
                    await unitOfWork.Complete();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to sync job state for Job {JobId}", jobId);
            }
        }
    }
}