using Hangfire;
using TorrenClou.Core.Enums;

namespace TorrenClou.Core.Interfaces
{
    public interface IJobRecoveryStrategy
    {
        JobType SupportedJobType { get; }
        IReadOnlyList<JobStatus> MonitoredStatuses { get; }
        Task<string?> RecoverJobAsync(IRecoverableJob job, IBackgroundJobClient backgroundJobClient);
    }
}
