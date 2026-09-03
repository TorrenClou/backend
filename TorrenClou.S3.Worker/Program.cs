using Serilog;
using TorrenClou.Application.Services;
using TorrenClou.Core.Entities.Jobs;
using TorrenClou.Core.Interfaces;
using TorrenClou.Core.Interfaces.Hangfire;
using TorrenClou.Core.Options;
using TorrenClou.Infrastructure.Extensions;
using TorrenClou.Infrastructure.Services;
using TorrenClou.Infrastructure.Services.Handlers;
using TorrenClou.Infrastructure.Services.Redis;
using TorrenClou.S3.Worker;
using TorrenClou.S3.Worker.Interfaces;
using TorrenClou.S3.Worker.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

const string ServiceName = "torrenclou-s3-worker";

// Bootstrap logger
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    // Configure Serilog
    builder.Configuration.ConfigureSharedSerilog(ServiceName, builder.Environment.EnvironmentName);
    builder.Services.AddSerilog();

    Log.Information("Starting {ServiceName}", ServiceName);

    // Infrastructure (Database, Redis, OpenTelemetry)
    builder.Services.AddSharedDatabase(builder.Configuration);
    builder.Services.AddSharedRedis(builder.Configuration);
    builder.Services.AddTorrenClouOpenTelemetry(ServiceName, builder.Configuration, builder.Environment);

    // Hangfire with S3 queue
    builder.Services.AddSharedHangfireBase(builder.Configuration);
    builder.Services.AddSharedHangfireServer(builder.Configuration, queues: ["s3", "default"]);

    // Job Service dependencies (IJobService is used by S3UploadJob for heartbeat/progress updates)
    builder.Services.AddSingleton<IJobCancellationSignal, RedisJobCancellationSignal>();
    builder.Services.AddScoped<IJobHandlerFactory, JobHandlerFactory>();
    builder.Services.AddScoped<IJobService, JobService>();

    // S3-Specific Services (NO BackblazeSettings - all credentials from UserStorageProfile)
    builder.Services.AddScoped<IS3JobService, S3JobService>();
    builder.Services.AddSingleton<IS3ResumableUploadServiceFactory, S3ResumableUploadServiceFactory>();
    builder.Services.AddScoped<IS3UploadJob, S3UploadJob>();

    // Shared Infrastructure Services
    builder.Services.AddScoped<IJobStatusService, TorrenClou.Infrastructure.Services.JobStatusService>();
    // DownloadCleanupService reads the delete-after-upload preference from the DB.
    builder.Services.AddScoped<IUserSettingsService, TorrenClou.Application.Services.UserSettingsService>();
    builder.Services.AddScoped<IDownloadCleanupService, DownloadCleanupService>();
    builder.Services.AddScoped<ITransferSpeedMetrics, TransferSpeedMetrics>();

    // Storage connection health + upload failover routing (JobService depends on it)
    builder.Services.AddStorageRoutingServices(builder.Configuration);

    // Hosted Services
    builder.Services.Configure<JobHealthMonitorOptions>(opts =>
    {
        opts.CheckInterval = TimeSpan.FromMinutes(2);
        opts.StaleJobThreshold = TimeSpan.FromMinutes(5);
    });
    builder.Services.AddHostedService<JobHealthMonitor<UserJob>>();
    builder.Services.AddHostedService<S3Worker>();

    // Configure host shutdown timeout to allow Hangfire graceful shutdown
    builder.Services.Configure<HostOptions>(opts =>
    {
        opts.ShutdownTimeout = TimeSpan.FromMinutes(6); // Longer than Hangfire's ServerTimeout
    });

    var host = builder.Build();

    Log.Information("{ServiceName} started successfully", ServiceName);
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "S3 Worker terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}
