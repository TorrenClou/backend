using Hangfire;
using TorreClou.Core.Entities.Jobs;
using TorreClou.Core.Enums;
using TorreClou.Core.Interfaces;
using TorreClou.GoogleDrive.Worker.Services;

namespace TorreClou.GoogleDrive.Worker.Services.Strategies
{
    public class GoogleDriveRecoveryStrategy : IJobRecoveryStrategy
    {
        public JobType SupportedJobType => JobType.Torrent;

        public string RecoverJob(IRecoverableJob job, IBackgroundJobClient backgroundJobClient)
        {
            // Cast to UserJob for job-specific properties
            var userJob = (UserJob)job;

            if (job.Status == JobStatus.PENDING_UPLOAD || job.Status == JobStatus.UPLOADING)
            {
                // Resume upload phase - job is ready for upload or already uploading
                userJob.CurrentState = "Recovering upload from interrupted state...";
                return backgroundJobClient.Enqueue<GoogleDriveUploadJob>(
                    service => service.ExecuteAsync(job.Id, CancellationToken.None));
            }

            // For other statuses, return null (not our responsibility)
            return null;
        }
    }
}

