using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TorreClou.Core.Entities.Jobs;
using TorreClou.Core.Interfaces;
using TorreClou.Infrastructure.Data;
using TorreClou.Infrastructure.Settings;

namespace TorreClou.Infrastructure.Services
{
    /// <summary>
    /// Service for managing job execution leases using PostgreSQL row-level locking.
    /// Ensures atomic lease acquisition to prevent duplicate job execution.
    /// </summary>
    public class JobLeaseService : IJobLeaseService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<JobLeaseService> _logger;
        private readonly JobLeaseSettings _settings;

        public JobLeaseService(
            ApplicationDbContext context,
            ILogger<JobLeaseService> logger,
            IOptions<JobLeaseSettings> settings)
        {
            _context = context;
            _logger = logger;
            _settings = settings.Value;
        }

        public async Task<LeaseAcquisitionResult> TryAcquireLeaseAsync(int jobId, string workerId, TimeSpan leaseDuration)
        {
            try
            {
                // Use raw SQL with FOR UPDATE NOWAIT to atomically lock the row
                // This ensures only one worker can acquire the lease at a time
                var job = await _context.UserJobs
                    .FromSqlRaw(
                        "SELECT * FROM \"UserJobs\" WHERE \"Id\" = {0} FOR UPDATE NOWAIT",
                        jobId)
                    .FirstOrDefaultAsync();

                if (job == null)
                {
                    _logger.LogWarning("Job not found for lease acquisition | JobId: {JobId}", jobId);
                    return LeaseAcquisitionResult.NotFound;
                }

                var now = DateTime.UtcNow;

                // Check if lease is already owned by another worker
                if (job.LeaseExpiresAt.HasValue && 
                    job.LeaseExpiresAt.Value > now && 
                    job.LeaseOwnerId != workerId)
                {
                    _logger.LogDebug(
                        "Lease already owned by another worker | JobId: {JobId} | CurrentOwner: {Owner} | RequestedBy: {Worker} | ExpiresAt: {Expires}",
                        jobId, job.LeaseOwnerId, workerId, job.LeaseExpiresAt);
                    return LeaseAcquisitionResult.AlreadyOwned;
                }

                // Acquire or take over the lease
                job.LeaseOwnerId = workerId;
                job.LeaseExpiresAt = now.Add(leaseDuration);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Lease acquired | JobId: {JobId} | WorkerId: {Worker} | ExpiresAt: {Expires}",
                    jobId, workerId, job.LeaseExpiresAt);

                return LeaseAcquisitionResult.Acquired;
            }
            catch (PostgresException ex) when (ex.SqlState == "55P03")
            {
                // Lock not available - another transaction is holding the lock
                _logger.LogDebug(
                    "Lock contention when acquiring lease | JobId: {JobId} | WorkerId: {Worker} | Error: {Error}",
                    jobId, workerId, ex.Message);
                return LeaseAcquisitionResult.LockContention;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error acquiring lease | JobId: {JobId} | WorkerId: {Worker}",
                    jobId, workerId);
                // Treat unknown errors as lock contention to be safe
                return LeaseAcquisitionResult.LockContention;
            }
        }

        public async Task<bool> RefreshLeaseAsync(int jobId, string workerId, TimeSpan leaseDuration)
        {
            try
            {
                var job = await _context.UserJobs.FindAsync(jobId);
                if (job == null)
                {
                    _logger.LogWarning("Job not found for lease refresh | JobId: {JobId}", jobId);
                    return false;
                }

                // Only refresh if we own the lease
                if (job.LeaseOwnerId != workerId)
                {
                    _logger.LogDebug(
                        "Cannot refresh lease - not owned by worker | JobId: {JobId} | CurrentOwner: {Owner} | RequestedBy: {Worker}",
                        jobId, job.LeaseOwnerId, workerId);
                    return false;
                }

                // Check if lease has expired
                if (job.LeaseExpiresAt.HasValue && job.LeaseExpiresAt.Value <= DateTime.UtcNow)
                {
                    _logger.LogWarning(
                        "Cannot refresh expired lease | JobId: {JobId} | WorkerId: {Worker} | ExpiredAt: {Expired}",
                        jobId, workerId, job.LeaseExpiresAt);
                    return false;
                }

                // Refresh the lease
                job.LeaseExpiresAt = DateTime.UtcNow.Add(leaseDuration);
                await _context.SaveChangesAsync();

                _logger.LogDebug(
                    "Lease refreshed | JobId: {JobId} | WorkerId: {Worker} | NewExpiresAt: {Expires}",
                    jobId, workerId, job.LeaseExpiresAt);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Error refreshing lease | JobId: {JobId} | WorkerId: {Worker}",
                    jobId, workerId);
                return false;
            }
        }

        public async Task ReleaseLeaseAsync(int jobId, string workerId)
        {
            try
            {
                var job = await _context.UserJobs.FindAsync(jobId);
                if (job == null)
                {
                    _logger.LogWarning("Job not found for lease release | JobId: {JobId}", jobId);
                    return;
                }

                // Only release if we own the lease
                if (job.LeaseOwnerId != workerId)
                {
                    _logger.LogDebug(
                        "Cannot release lease - not owned by worker | JobId: {JobId} | CurrentOwner: {Owner} | RequestedBy: {Worker}",
                        jobId, job.LeaseOwnerId, workerId);
                    return;
                }

                // Release the lease
                job.LeaseOwnerId = null;
                job.LeaseExpiresAt = null;
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Lease released | JobId: {JobId} | WorkerId: {Worker}",
                    jobId, workerId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Error releasing lease | JobId: {JobId} | WorkerId: {Worker}",
                    jobId, workerId);
            }
        }

        public async Task<bool> IsLeaseExpiredAsync(int jobId)
        {
            try
            {
                var job = await _context.UserJobs.FindAsync(jobId);
                if (job == null)
                {
                    return true; // Consider non-existent jobs as "expired" (available)
                }

                // Lease is expired if it doesn't exist or expiration time has passed
                return !job.LeaseExpiresAt.HasValue || job.LeaseExpiresAt.Value <= DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking lease expiration | JobId: {JobId}", jobId);
                return true; // On error, assume expired to allow retry
            }
        }
    }
}

