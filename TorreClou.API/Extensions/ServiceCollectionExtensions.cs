using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TorreClou.API.Middleware;

namespace TorreClou.API.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddOpenApi();

            services.AddExceptionHandler<GlobalExceptionHandler>();
            services.AddProblemDetails();

            // Note: Datadog APM is configured via UseDatadog() extension in Program.cs
            // Automatic instrumentation for ASP.NET Core, HttpClient, EF Core, Redis, etc.
            // is handled by the Datadog.Trace.Bundle package

            return services;
        }
    }
}
