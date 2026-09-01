using Microsoft.Extensions.DependencyInjection;
using TorreClou.Application.Services;
using TorreClou.Application.Services.OAuth;
using TorreClou.Application.Services.Setup;
using TorreClou.Application.Services.Storage;
using TorreClou.Application.Services.Torrent;
using TorreClou.Core.Interfaces;

namespace TorreClou.Application.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ITorrentAnalysisService, TorrentAnalysisService>();
            services.AddScoped<ITrackerScraper, UdpTrackerScraper>();
            services.AddScoped<ITorrentService, TorrentService>();
            services.AddScoped<ITorrentHealthService, TorrentHealthService>();

            services.AddScoped<IJobService, JobService>();
            services.AddScoped<IGoogleDriveAuthService, GoogleDriveAuthService>();
            services.AddScoped<IOAuthStateService, OAuthStateService>();

            services.AddScoped<IStorageProfilesService, StorageProfilesService>();
            services.AddScoped<IS3StorageService, S3StorageService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IUserSettingsService, UserSettingsService>();

            // The settings cache is a singleton so every scope in the process shares one
            // copy of a row that is read constantly and written rarely.
            services.AddSingleton<SystemSettingsCache>();
            services.AddSingleton<IPasswordHasher, PasswordHasherService>();
            services.AddScoped<ISystemSettingsService, SystemSettingsService>();
            services.AddScoped<IOAuthService, OAuthService>();

            return services;
        }
    }
}
