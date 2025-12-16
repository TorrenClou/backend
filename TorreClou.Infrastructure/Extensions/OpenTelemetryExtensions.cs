using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace TorreClou.Infrastructure.Extensions
{
    /// <summary>
    /// Extension methods for configuring OpenTelemetry observability.
    /// </summary>
    public static class OpenTelemetryExtensions
    {
        /// <summary>
        /// Configures OpenTelemetry for applications (API and Workers).
        /// </summary>
        public static IServiceCollection AddTorreClouOpenTelemetry(
            this IServiceCollection services,
            string serviceName,
            IConfiguration configuration,
            IHostEnvironment environment,
            bool includeAspNetCoreInstrumentation = false)
        {
            var observabilityConfig = configuration.GetSection("Observability");
            var enablePrometheus = observabilityConfig.GetValue<bool>("EnablePrometheus", includeAspNetCoreInstrumentation);
            var enableTracing = observabilityConfig.GetValue<bool>("EnableTracing", true);
            var otlpEndpoint = observabilityConfig["OtlpEndpoint"];
            var otlpHeaders = observabilityConfig["OtlpHeaders"]; // Add this

            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService(serviceName)
                    .AddAttributes(new Dictionary<string, object>
                    {
                        ["deployment.environment"] = environment.EnvironmentName
                    }))
                .WithMetrics(metrics =>
                {
                    if (includeAspNetCoreInstrumentation)
                    {
                        metrics.AddAspNetCoreInstrumentation();
                    }
                    metrics
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation()
                        .AddProcessInstrumentation();

                    if (enablePrometheus)
                    {
                        metrics.AddPrometheusExporter();
                    }

                    if (!string.IsNullOrEmpty(otlpEndpoint))
                    {
                        metrics.AddOtlpExporter(options =>
                        {
                            options.Endpoint = new Uri(otlpEndpoint);
                            // Add headers if provided
                            if (!string.IsNullOrEmpty(otlpHeaders))
                            {
                                options.Headers = otlpHeaders;
                            }
                        });
                    }
                });

            if (enableTracing)
            {
                services.AddOpenTelemetry()
                    .WithTracing(tracing =>
                    {
                        if (includeAspNetCoreInstrumentation)
                        {
                            tracing.AddAspNetCoreInstrumentation();
                        }
                        tracing
                            .AddHttpClientInstrumentation()
                            .AddEntityFrameworkCoreInstrumentation(options =>
                            {
                                options.SetDbStatementForText = true;
                            })
                            .AddRedisInstrumentation();

                        if (!string.IsNullOrEmpty(otlpEndpoint))
                        {
                            tracing.AddOtlpExporter(options =>
                            {
                                options.Endpoint = new Uri(otlpEndpoint);
                                // Add headers if provided
                                if (!string.IsNullOrEmpty(otlpHeaders))
                                {
                                    options.Headers = otlpHeaders;
                                }
                            });
                        }
                    });
            }

            return services;
        }
    }
}
