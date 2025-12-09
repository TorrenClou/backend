using TorreClou.Core.DTOs.Jobs;
using TorreClou.Core.Shared;

namespace TorreClou.Core.Interfaces
{
    public interface IJobService
    {
        Task<Result<JobCreationResult>> CreateAndDispatchJobAsync(int invoiceId, int userId);
    }
}

