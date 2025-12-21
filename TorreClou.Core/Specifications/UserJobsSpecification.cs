using TorreClou.Core.Entities.Jobs;
using TorreClou.Core.Enums;

namespace TorreClou.Core.Specifications
{
    public class UserJobsSpecification : BaseSpecification<UserJob>
    {
        public UserJobsSpecification(int userId, int pageNumber, int pageSize, JobStatus? status = null, UserRole? userRole = null)
            : base(job => 
                job.UserId == userId && 
                (status == null || job.Status == status) &&
                // Filter logic: 
                // - Sync type jobs are internal - only visible to Admin/Support
                // - SYNCING and SYNC_RETRY statuses are for Sync jobs - regular users should never see them
                // - Regular users only see Torrent type jobs with statuses other than SYNCING/SYNC_RETRY
                (userRole == null || 
                 userRole == UserRole.Admin || 
                 userRole == UserRole.Support ||
                 (job.Type == JobType.Torrent && 
                  job.Status != JobStatus.SYNCING && 
                  job.Status != JobStatus.SYNC_RETRY))) // Regular users: Torrent jobs only, excluding SYNCING/SYNC_RETRY
        {
            AddInclude(job => job.StorageProfile);
            AddInclude(job => job.RequestFile);
            AddOrderByDescending(job => job.CreatedAt);
            ApplyPaging((pageNumber - 1) * pageSize, pageSize);
        }
    }
}
