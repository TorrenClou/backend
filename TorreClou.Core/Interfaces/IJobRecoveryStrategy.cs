using Hangfire;
using TorreClou.Core.Enums;

namespace TorreClou.Core.Interfaces
{
    /// <summary>
    /// Strategy interface for recovering orphaned jobs of specific types.
    /// Each job type (Torrent, DirectDownload, etc.) should implement this interface.
    /// </summary>
    public interface IJobRecoveryStrategy
    {
        /// <summary>
        /// The job type this strategy handles.
        /// </summary>
        JobType SupportedJobType { get; }

        /// <summary>
        /// Recovers an orphaned job by re-enqueuing it to Hangfire.
        /// </summary>
        /// <param name="job">The job to recover (implements IRecoverableJob)</param>
        /// <param name="backgroundJobClient">Hangfire client for enqueuing</param>
        /// <returns>The new Hangfire job ID</returns>
        string RecoverJob(IRecoverableJob job, IBackgroundJobClient backgroundJobClient);
    }
}
