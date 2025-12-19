using Hangfire;
using TorreClou.Core.Entities.Jobs;
using TorreClou.Core.Enums;
using TorreClou.Core.Interfaces;

namespace TorreClou.Worker.Services.Strategies
{
 
    public class TorrentRecoveryStrategy : IJobRecoveryStrategy
    {
        public JobType SupportedJobType => JobType.Torrent;

        public string RecoverJob(IRecoverableJob job, IBackgroundJobClient backgroundJobClient)
        {
            // Cast to UserJob for torrent-specific properties
            var userJob = (UserJob)job;

            if (job.Status == JobStatus.PENDING_UPLOAD || 
                job.Status == JobStatus.UPLOADING ||
                job.Status == JobStatus.UPLOAD_RETRY)
            {
                // Resume upload phase - job is ready for upload or already uploading
                // Note: PENDING_UPLOAD jobs should be picked up by the upload worker via Redis stream
                // This recovery is for orphaned UPLOADING jobs
                userJob.CurrentState = "Recovering upload from interrupted state...";
                // Don't enqueue to download job - upload worker should handle this
                return null; // Let upload worker handle via Redis stream
            }
            else if (job.Status == JobStatus.DOWNLOADING ||
                     job.Status == JobStatus.TORRENT_DOWNLOAD_RETRY ||
                     job.Status == JobStatus.TORRENT_FAILED ||
                     job.Status == JobStatus.SYNCING ||
                     job.Status == JobStatus.SYNC_RETRY ||
                     job.Status == JobStatus.QUEUED)
            {
                // Resume download/sync phase - reset to QUEUED and re-enqueue
                job.Status = JobStatus.QUEUED;
                userJob.CurrentState = "Recovering download from interrupted state...";
                return backgroundJobClient.Enqueue<TorrentDownloadJob>(
                    service => service.ExecuteAsync(job.Id, CancellationToken.None));
            }
            else
            {
                // Unknown state - don't recover
                return null;
            }
        }
    }
}
