using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TorreClou.Core.Interfaces;
using TorreClou.Infrastructure.Services;
using TorreClou.Application.Services.Google_Drive;
using TorreClou.Infrastructure.Services.Drive;
using TorreClou.Infrastructure.Services.Handlers;
using TorreClou.Infrastructure.Services.Health;
using TorreClou.Infrastructure.Services.Redis;
using TorreClou.Core.Options;

namespace TorreClou.Infrastructure.Extensions
{
    /// <summary>
    /// Registers API-specific infrastructure services.
    /// Database, Redis, and Repository registrations are handled by
    /// AddSharedDatabase() and AddSharedRedis() in SharedConfigurationExtensions.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<ITokenService, TokenService>();

            // Google Drive Services (credentials configured per-user via API)
            services.AddScoped<IGoogleDriveJobService, GoogleDriveJobService>();
            services.AddScoped<IGoogleDriveService, GoogleDriveService>();

            // Upload Progress Context (scoped per Hangfire job)
            services.AddScoped<IUploadProgressContext, UploadProgressContext>();

            // Transfer Speed Metrics (singleton for metrics collection)
            services.AddSingleton<ITransferSpeedMetrics, TransferSpeedMetrics>();

            // Job Status Service (timeline tracking)
            services.AddScoped<IJobStatusService, JobStatusService>();

            // Health Check Service
            services.AddScoped<IHealthCheckService, HealthCheckService>();

            // Storage connection health + upload failover routing
            services.AddStorageRoutingServices(configuration);

            // Google API Client (for OAuth token exchange and user info)
            services.AddScoped<IGoogleApiClient, GoogleApiClient>();

            // Job Handlers (Strategy Pattern for decoupled job processing)
            // Storage Provider Handlers
            services.AddScoped<IStorageProviderHandler, GoogleDriveStorageProviderHandler>();
            services.AddScoped<IStorageProviderHandler, S3StorageProviderHandler>();
            
            // Job Type Handlers
            services.AddScoped<IJobTypeHandler, TorrentJobTypeHandler>();
            
            // Job Cancellation Handlers
            services.AddScoped<IJobCancellationHandler, TorrentCancellationHandler>();
            
            // Job Handler Factory
            services.AddScoped<IJobHandlerFactory, JobHandlerFactory>();

            // Distributed cancellation signal (Redis-backed, singleton — stateless Redis wrapper)
            services.AddSingleton<IJobCancellationSignal, RedisJobCancellationSignal>();

            return services;
        }

        /// <summary>
        /// Registers storage connection health probing and upload failover routing.
        /// Called by the API through <see cref="AddInfrastructureServices"/>, and directly
        /// by the upload workers, which build their own service collections.
        /// </summary>
        public static IServiceCollection AddStorageRoutingServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<UploadRoutingOptions>(
                configuration.GetSection(UploadRoutingOptions.SectionName));

            // Probe dependencies. TryAdd keeps this safe in hosts (the API, the Drive
            // worker) that already register them; the S3 worker gets them from here.
            services.AddHttpClient();
            services.TryAddScoped<IGoogleDriveJobService, GoogleDriveJobService>();
            services.TryAddScoped<IUploadProgressContext, UploadProgressContext>();

            // Probes are resolved by ProviderType inside StorageProfileHealthService.
            services.AddScoped<IStorageHealthProbe, GoogleDriveHealthProbe>();
            services.AddScoped<IStorageHealthProbe, S3HealthProbe>();

            services.AddScoped<IStorageProfileHealthService, StorageProfileHealthService>();
            services.AddScoped<IUploadRoutingService, UploadRoutingService>();

            return services;
        }
    }
}
