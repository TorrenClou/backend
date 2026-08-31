using TorreClou.Core.DTOs.Common;
using TorreClou.Core.DTOs.Jobs;
using TorreClou.Core.DTOs.Torrents;
using TorreClou.Core.Entities.Jobs;
using TorreClou.Core.Enums;

namespace TorreClou.Core.Interfaces
{
    public interface IJobService
    {
        Task<JobCreationResult> CreateAndDispatchJobAsync(int torrentFileId, int userId, string[]? selectedFiles, int storageProfileId);

        /// <summary>
        /// Creates and dispatches a job per item. Items are independent: a torrent that
        /// fails validation, is a duplicate, or already has an active job is reported in
        /// its own result and does not stop the others.
        /// </summary>
        Task<BatchJobCreationResultDto> CreateAndDispatchJobsAsync(int userId, CreateJobsRequestDto request);
        Task<PaginatedResult<JobDto>> GetUserJobsAsync(int userId, int pageNumber, int pageSize, JobStatus? status = null);
        Task<JobDto> GetJobByIdAsync(int userId, int jobId, UserRole? userRole = null);
        Task<JobStatisticsDto> GetUserJobStatisticsAsync(int userId);

        Task<IReadOnlyList<UserJob>> GetActiveJobsByStorageProfileIdAsync(int storageProfileId);

        /// <summary>
        /// Retries a job. Pass <paramref name="targetStorageProfileId"/> to send the retry
        /// to a different storage profile than the one that failed.
        /// </summary>
        Task RetryJobAsync(int jobId, int userId, UserRole? userRole = null, int? targetStorageProfileId = null);

        /// <summary>
        /// Points a job at a different storage profile before its upload starts. Rejected
        /// while an upload is in flight — retry with a target profile instead.
        /// </summary>
        /// <param name="allowFailover">
        /// When false, the job stays on this profile even if it becomes unhealthy, and
        /// fails rather than being rerouted automatically.
        /// </param>
        Task<JobDto> ChangeJobStorageProfileAsync(
            int jobId,
            int userId,
            int storageProfileId,
            bool allowFailover = true,
            UserRole? userRole = null);

        Task CancelJobAsync(int jobId, int userId, UserRole? userRole = null);

        // Worker-facing job state updates
        Task UpdateJobStartedAtAsync(UserJob job);
        Task UpdateJobProgressAsync(UserJob job, long bytesUploaded);
        Task UpdateHeartbeatAsync(int jobId);
    }
}
