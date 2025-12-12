using TorreClou.Core.Entities.Jobs;
using TorreClou.Core.Enums;
using TorreClou.Core.Interfaces;
using TorreClou.Core.Specifications;
using Microsoft.Extensions.Logging;

namespace TorreClou.Infrastructure.Workers
{
    /// <summary>
    /// Abstract base class for Hangfire jobs that process UserJob entities.
    /// Implements Template Method pattern for consistent job lifecycle management.
    /// </summary>
    /// <typeparam name="TJob">The concrete job type for logger categorization.</typeparam>
    public abstract class BaseJob<TJob> where TJob : class
    {
        protected readonly IUnitOfWork UnitOfWork;
        protected readonly ILogger<TJob> Logger;

        /// <summary>
        /// Log prefix for consistent logging (e.g., "[DOWNLOAD]", "[UPLOAD]").
        /// </summary>
        protected abstract string LogPrefix { get; }

        protected BaseJob(IUnitOfWork unitOfWork, ILogger<TJob> logger)
        {
            UnitOfWork = unitOfWork;
            Logger = logger;
        }

        /// <summary>
        /// Template method that orchestrates the job execution lifecycle.
        /// Subclasses should override ExecuteCoreAsync for specific job logic.
        /// </summary>
        public async Task ExecuteAsync(int jobId, CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("{LogPrefix} Starting job | JobId: {JobId}", LogPrefix, jobId);

            UserJob? job = null;

            try
            {
                // 1. Load job from database
                job = await LoadJobAsync(jobId);
                if (job == null) return;

                // 2. Check if already completed or cancelled
                if (IsJobTerminated(job))
                {
                    Logger.LogInformation("{LogPrefix} Job already finished | JobId: {JobId} | Status: {Status}", 
                        LogPrefix, jobId, job.Status);
                    return;
                }

                // 3. Execute the core job logic
                await ExecuteCoreAsync(job, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Logger.LogWarning("{LogPrefix} Job cancelled | JobId: {JobId}", LogPrefix, jobId);
                if (job != null)
                {
                    await OnJobCancelledAsync(job);
                }
                throw; // Let Hangfire handle the cancellation
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "{LogPrefix} Fatal error | JobId: {JobId}", LogPrefix, jobId);

                if (job != null)
                {
                    await OnJobErrorAsync(job, ex);
                    await MarkJobFailedAsync(job, ex.Message);
                }

                throw; // Let Hangfire retry if attempts remain
            }
        }

        /// <summary>
        /// Core job execution logic. Override in derived classes.
        /// </summary>
        protected abstract Task ExecuteCoreAsync(UserJob job, CancellationToken cancellationToken);

        /// <summary>
        /// Configure the specification with job-specific includes.
        /// Override in derived classes to add related entities.
        /// </summary>
        protected abstract void ConfigureSpecification(BaseSpecification<UserJob> spec);

        /// <summary>
        /// Hook for cleanup when job is cancelled. Override for specific cleanup logic.
        /// </summary>
        protected virtual Task OnJobCancelledAsync(UserJob job)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Hook for cleanup when job encounters an error. Override for specific cleanup logic.
        /// </summary>
        protected virtual Task OnJobErrorAsync(UserJob job, Exception exception)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Loads the job from the database with configured includes.
        /// </summary>
        protected async Task<UserJob?> LoadJobAsync(int jobId)
        {
            var spec = new BaseSpecification<UserJob>(j => j.Id == jobId);
            ConfigureSpecification(spec);

            var job = await UnitOfWork.Repository<UserJob>().GetEntityWithSpec(spec);

            if (job == null)
            {
                Logger.LogError("{LogPrefix} Job not found | JobId: {JobId}", LogPrefix, jobId);
            }

            return job;
        }

        /// <summary>
        /// Checks if the job is in a terminal state (COMPLETED or CANCELLED).
        /// </summary>
        protected bool IsJobTerminated(UserJob job)
        {
            return job.Status == JobStatus.COMPLETED || job.Status == JobStatus.CANCELLED;
        }

        /// <summary>
        /// Updates the job's heartbeat and current state.
        /// </summary>
        protected async Task UpdateHeartbeatAsync(UserJob job, string? state = null)
        {
            try
            {
                job.LastHeartbeat = DateTime.UtcNow;
                if (state != null)
                {
                    job.CurrentState = state;
                }
                await UnitOfWork.Complete();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "{LogPrefix} Failed to update heartbeat | JobId: {JobId}", LogPrefix, job.Id);
            }
        }

        /// <summary>
        /// Marks the job as failed with an error message.
        /// </summary>
        protected async Task MarkJobFailedAsync(UserJob job, string errorMessage)
        {
            try
            {
                job.Status = JobStatus.FAILED;
                job.ErrorMessage = errorMessage;
                job.CompletedAt = DateTime.UtcNow;
                await UnitOfWork.Complete();

                Logger.LogError("{LogPrefix} Job marked as failed | JobId: {JobId} | Error: {Error}", 
                    LogPrefix, job.Id, errorMessage);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "{LogPrefix} Failed to mark job as failed | JobId: {JobId}", LogPrefix, job.Id);
            }
        }
    }
}

