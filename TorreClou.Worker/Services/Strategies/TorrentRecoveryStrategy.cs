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

            if (job.Status == JobStatus.UPLOADING)
            {
                // Resume upload phase
                userJob.CurrentState = "Recovering upload from interrupted state...";
                return backgroundJobClient.Enqueue<TorrentUploadJob>(
                    service => service.ExecuteAsync(job.Id, CancellationToken.None));
            }
            else
            {
                // Resume download phase (PROCESSING or unknown state)
                job.Status = JobStatus.QUEUED;
                userJob.CurrentState = "Recovering download from interrupted state...";
                return backgroundJobClient.Enqueue<TorrentDownloadJob>(
                    service => service.ExecuteAsync(job.Id, CancellationToken.None));
            }
        }
    }
}
