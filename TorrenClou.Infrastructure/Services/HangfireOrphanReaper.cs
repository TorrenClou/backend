using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TorrenClou.Infrastructure.Services
{
    /// <summary>
    /// Clears Hangfire jobs left in Processing by a server that no longer exists.
    ///
    /// A worker container that is killed rather than shut down cleanly leaves its
    /// in-flight jobs marked Processing. Nothing reclaims them: the record names a server
    /// that has gone, so no worker will ever finish it, and the dashboard keeps counting
    /// it as in-flight. Enough of them and Processing exceeds the number of workers that
    /// exist, which makes the queue impossible to reason about.
    ///
    /// Only records whose owning server is absent from the live registry are touched, and
    /// only after a grace period, so a server that is merely restarting is left alone.
    /// </summary>
    public class HangfireOrphanReaper(
        ILogger<HangfireOrphanReaper> logger) : BackgroundService
    {
        private const string LogPrefix = "[REAPER]";

        /// <summary>
        /// How long a Processing record must have been running before it is eligible.
        /// Comfortably longer than Hangfire's ServerTimeout (2 minutes) so a server that
        /// is mid-restart is never mistaken for one that is gone.
        /// </summary>
        private static readonly TimeSpan OrphanGracePeriod = TimeSpan.FromMinutes(10);

        private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(10);

        /// <summary>Bound on records inspected per sweep, so a large backlog cannot stall startup.</summary>
        private const int MaxPerSweep = 500;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("{LogPrefix} Started | Interval: {Interval} | Grace: {Grace}",
                LogPrefix, SweepInterval, OrphanGracePeriod);

            // A restart is exactly when orphans are created, so sweep on the way up too.
            await SweepAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(SweepInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                await SweepAsync(stoppingToken);
            }
        }

        private Task SweepAsync(CancellationToken cancellationToken)
        {
            try
            {
                var monitoringApi = JobStorage.Current?.GetMonitoringApi();
                if (monitoringApi == null)
                {
                    logger.LogDebug("{LogPrefix} No Hangfire storage configured, skipping", LogPrefix);
                    return Task.CompletedTask;
                }

                var liveServers = monitoringApi.Servers()
                    .Select(s => s.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var processing = monitoringApi.ProcessingJobs(0, MaxPerSweep);
                if (processing.Count == 0) return Task.CompletedTask;

                var cutoff = DateTime.UtcNow - OrphanGracePeriod;
                var reaped = 0;

                foreach (var entry in processing)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    var dto = entry.Value;
                    if (dto == null) continue;

                    // Owned by a server that is still reporting: genuinely in flight.
                    if (dto.ServerId != null && liveServers.Contains(dto.ServerId)) continue;

                    // Too recent to judge — a server may be coming back.
                    if (dto.StartedAt.HasValue && dto.StartedAt.Value > cutoff) continue;

                    try
                    {
                        BackgroundJob.Delete(entry.Key);
                        reaped++;

                        logger.LogInformation(
                            "{LogPrefix} Removed orphaned Processing job | HangfireJobId: {HangfireJobId} | DeadServer: {ServerId} | StartedAt: {StartedAt}",
                            LogPrefix, entry.Key, dto.ServerId ?? "(none)", dto.StartedAt);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "{LogPrefix} Could not remove orphaned job | HangfireJobId: {HangfireJobId}",
                            LogPrefix, entry.Key);
                    }
                }

                if (reaped > 0)
                {
                    logger.LogWarning("{LogPrefix} Swept {Reaped} orphaned Processing job(s) | Inspected: {Inspected} | LiveServers: {Servers}",
                        LogPrefix, reaped, processing.Count, liveServers.Count);
                }
                else
                {
                    logger.LogDebug("{LogPrefix} Nothing to sweep | Inspected: {Inspected}", LogPrefix, processing.Count);
                }
            }
            catch (Exception ex)
            {
                // Housekeeping must never take the host down.
                logger.LogError(ex, "{LogPrefix} Sweep failed", LogPrefix);
            }

            return Task.CompletedTask;
        }
    }
}
