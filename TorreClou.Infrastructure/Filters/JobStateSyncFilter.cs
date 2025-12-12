using Amazon.Runtime.Internal.Util;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TorreClou.Core.Entities.Jobs;
using TorreClou.Core.Enums;
using TorreClou.Core.Interfaces;

namespace TorreClou.Worker.Filters
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

                if (job != null && job.Status != JobStatus.FAILED)
                {
                    logger.LogError("[Filter] Marking job {JobId} as FAILED due to Hangfire failure.", jobId);
                    job.Status = JobStatus.FAILED;
                    job.ErrorMessage = $"System Failure: {error}";
                    job.CompletedAt = DateTime.UtcNow;
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