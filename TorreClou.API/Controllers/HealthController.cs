using System.Diagnostics;
using System.Reflection;
using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using TorreClou.API.Controllers;
using TorreClou.Infrastructure.Data;

namespace TorreClou.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ApplicationDbContext _dbContext;
        private readonly IMonitoringApi _hangfireMonitoring;
        private readonly ILogger<HealthController> _logger;

        public HealthController(
            IConnectionMultiplexer redis,
            ApplicationDbContext dbContext,
            IMonitoringApi hangfireMonitoring,
            ILogger<HealthController> logger)
        {
            _redis = redis;
            _dbContext = dbContext;
            _hangfireMonitoring = hangfireMonitoring;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetHealth()
        {
            var health = new HealthResponse
            {
                Timestamp = DateTime.UtcNow,
                Version = GetVersion(),
                Workers = await GetWorkersStatusAsync(),
                Database = await GetDatabaseStatusAsync(),
                Storage = GetStorageStatus(),
                Alerts = new List<Alert>()
            };

            // Generate alerts
            if (health.Storage.UsagePercent >= 90)
            {
                health.Alerts.Add(new Alert
                {
                    Level = "warning",
                    Message = $"Storage usage is above 90% ({health.Storage.UsagePercent:F1}%)",
                    Timestamp = DateTime.UtcNow
                });
            }

            if (health.Database.Status != "healthy")
            {
                health.Alerts.Add(new Alert
                {
                    Level = "error",
                    Message = $"Database connection failed: {health.Database.Error}",
                    Timestamp = DateTime.UtcNow
                });
            }

            foreach (var worker in health.Workers)
            {
                if (worker.Value != "healthy")
                {
                    health.Alerts.Add(new Alert
                    {
                        Level = "warning",
                        Message = $"Worker {worker.Key} is {worker.Value}",
                        Timestamp = DateTime.UtcNow
                    });
                }
            }

            // Determine overall status
            if (health.Alerts.Any(a => a.Level == "error") || health.Database.Status != "healthy")
            {
                health.Status = "unhealthy";
            }
            else if (health.Alerts.Any(a => a.Level == "warning"))
            {
                health.Status = "degraded";
            }
            else
            {
                health.Status = "healthy";
            }

            var statusCode = health.Status switch
            {
                "unhealthy" => 503,
                "degraded" => 200,
                _ => 200
            };

            return StatusCode(statusCode, health);
        }

        private string GetVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return version?.ToString() ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        private async Task<Dictionary<string, string>> GetWorkersStatusAsync()
        {
            var workers = new Dictionary<string, string>();

            try
            {
                var db = _redis.GetDatabase();

                // Check Redis stream consumer groups
                var streamKeys = new[]
                {
                    ("jobs:stream", "torrent-workers", "torrent-worker"),
                    ("uploads:googledrive:stream", "googledrive-workers", "googledrive-worker"),
                    ("uploads:awss3:stream", "s3-workers", "s3-worker")
                };

                foreach (var (streamKey, consumerGroup, workerName) in streamKeys)
                {
                    try
                    {
                        // Check if stream exists
                        var streamInfo = await db.StreamInfoAsync(streamKey);
                        if (streamInfo.Length == 0)
                        {
                            workers[workerName] = "no_stream";
                            continue;
                        }

                        // Check consumer group
                        var groupInfo = await db.StreamGroupInfoAsync(streamKey);
                        if (groupInfo.Length == 0)
                        {
                            workers[workerName] = "no_consumer_group";
                            continue;
                        }

                        // Check for active consumers (consumers with pending messages or recent activity)
                        var consumers = await db.StreamConsumerInfoAsync(streamKey, consumerGroup);
                        var hasActiveConsumers = consumers.Any(c => c.PendingMessageCount > 0 || 
                            c.IdleTimeInMilliseconds < 60000); // Active if idle < 1 minute

                        workers[workerName] = hasActiveConsumers ? "healthy" : "idle";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to check worker status for {Worker}", workerName);
                        workers[workerName] = "unknown";
                    }
                }

                // Check Hangfire servers
                try
                {
                    var servers = _hangfireMonitoring.Servers();
                    var serverNames = servers.Select(s => s.Name).ToList();
                    
                    // Check if expected servers are running
                    var expectedServers = new[] { "torrent-worker", "googledrive-worker", "s3-worker" };
                    foreach (var expected in expectedServers)
                    {
                        if (!workers.ContainsKey(expected))
                        {
                            var hasServer = serverNames.Any(s => s.Contains(expected, StringComparison.OrdinalIgnoreCase));
                            workers[expected] = hasServer ? "healthy" : "offline";
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check Hangfire servers");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get workers status");
                workers["error"] = ex.Message;
            }

            return workers;
        }

        private async Task<DatabaseStatus> GetDatabaseStatusAsync()
        {
            var status = new DatabaseStatus();

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var canConnect = await _dbContext.Database.CanConnectAsync();
                stopwatch.Stop();

                if (canConnect)
                {
                    status.Status = "healthy";
                    status.ResponseTime = $"{stopwatch.ElapsedMilliseconds}ms";

                    // Check pending migrations
                    var pendingMigrations = await _dbContext.Database.GetPendingMigrationsAsync();
                    if (pendingMigrations.Any())
                    {
                        status.Status = "degraded";
                        status.MigrationsPending = pendingMigrations.Count();
                    }
                }
                else
                {
                    status.Status = "unhealthy";
                    status.Error = "Cannot connect to database";
                }
            }
            catch (Exception ex)
            {
                status.Status = "unhealthy";
                status.Error = ex.Message;
                _logger.LogError(ex, "Database health check failed");
            }

            return status;
        }

        private StorageStatus GetStorageStatus()
        {
            var status = new StorageStatus();

            try
            {
                // Check /app/downloads mount point
                var downloadPath = "/app/downloads";
                
                if (!Directory.Exists(downloadPath))
                {
                    // Fallback to current directory if /app/downloads doesn't exist (for local dev)
                    downloadPath = Directory.GetCurrentDirectory();
                }

                var driveInfo = new DriveInfo(Path.GetPathRoot(downloadPath) ?? downloadPath);
                
                status.TotalBytes = driveInfo.TotalSize;
                status.UsedBytes = driveInfo.TotalSize - driveInfo.AvailableFreeSpace;
                status.AvailableBytes = driveInfo.AvailableFreeSpace;
                status.UsagePercent = (double)status.UsedBytes / status.TotalBytes * 100;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get storage status");
                status.Error = ex.Message;
            }

            return status;
        }

        public class HealthResponse
        {
            public string Status { get; set; } = "unknown";
            public string Version { get; set; } = "unknown";
            public DateTime Timestamp { get; set; }
            public Dictionary<string, string> Workers { get; set; } = new();
            public DatabaseStatus Database { get; set; } = new();
            public StorageStatus Storage { get; set; } = new();
            public List<Alert> Alerts { get; set; } = new();
        }

        public class DatabaseStatus
        {
            public string Status { get; set; } = "unknown";
            public string? ResponseTime { get; set; }
            public string? Error { get; set; }
            public int? MigrationsPending { get; set; }
        }

        public class StorageStatus
        {
            public long TotalBytes { get; set; }
            public long UsedBytes { get; set; }
            public long AvailableBytes { get; set; }
            public double UsagePercent { get; set; }
            public string? Error { get; set; }
        }

        public class Alert
        {
            public string Level { get; set; } = "info";
            public string Message { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
        }
    }
}

