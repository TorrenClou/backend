using Hangfire;
using TorreClou.Core.Enums;

namespace TorreClou.Core.Interfaces
{
    public interface IJobRecoveryStrategy
    {
        JobType SupportedJobType { get; }
        IReadOnlyList<JobStatus> MonitoredStatuses { get; }
        Task<string?> RecoverJobAsync(IRecoverableJob job, IBackgroundJobClient backgroundJobClient);
    }
}
