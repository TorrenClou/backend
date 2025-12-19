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

            if (job.Status == JobStatus.PENDING_UPLOAD || 
                job.Status == JobStatus.UPLOADING ||
                job.Status == JobStatus.UPLOAD_RETRY ||
                job.Status == JobStatus.UPLOAD_FAILED ||
                job.Status == JobStatus.GOOGLE_DRIVE_FAILED)
            {
                // Resume upload phase - job is ready for upload, uploading, or failed during upload
                // For failure states, we'll retry the upload
                userJob.CurrentState = "Recovering upload from interrupted state...";
                return backgroundJobClient.Enqueue<GoogleDriveUploadJob>(
                    service => service.ExecuteAsync(job.Id, CancellationToken.None));
            }

            // For other statuses (download/sync phases), return null (not our responsibility)
            return null;
        }
    }
}

