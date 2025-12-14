using System.Reflection;
using Datadog.Trace;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Datadog.Logs;

namespace TorreClou.Infrastructure.Extensions
{
    /// <summary>
    /// Extension methods for configuring Datadog APM and logging integration.
    /// </summary>
    public static class DatadogExtensions
    {
        /// <summary>
        /// Configures Datadog tracing for the application.
        /// Sets up automatic instrumentation for HTTP, EF Core, Redis, and Hangfire.
        /// Note: With Datadog.Trace.Bundle, most configuration is done via environment variables.
        /// This method sets essential environment variables if not already set.
        /// </summary>
        /// <param name="builder">The host builder</param>
        /// <param name="serviceName">The service name to use in Datadog</param>
        /// <returns>The host builder for chaining</returns>
        public static IHostBuilder UseDatadogTracing(this IHostBuilder builder, string serviceName)
        {
            ConfigureDatadogEnvironment(serviceName);
            return builder;
        }

        /// <summary>
        /// Configures Datadog tracing for HostApplicationBuilder (used in worker services).
        /// </summary>
        /// <param name="builder">The host application builder</param>
        /// <param name="serviceName">The service name to use in Datadog</param>
        /// <returns>The host application builder for chaining</returns>
        public static HostApplicationBuilder UseDatadogTracing(this HostApplicationBuilder builder, string serviceName)
        {
            ConfigureDatadogEnvironment(serviceName);
            return builder;
        }

        private static void ConfigureDatadogEnvironment(string serviceName)
        {
            // Set environment variables for Datadog tracer if not already set
            // These are read by the Datadog.Trace.Bundle auto-instrumentation
            SetEnvIfNotExists("DD_SERVICE", serviceName);
            SetEnvIfNotExists("DD_ENV", "development");
            SetEnvIfNotExists("DD_VERSION", Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0");
            SetEnvIfNotExists("DD_LOGS_INJECTION", "true");
            SetEnvIfNotExists("DD_TRACE_AGENT_PORT", "8126");

            // Enable specific integrations
            SetEnvIfNotExists("DD_TRACE_ASPNETCORE_ENABLED", "true");
            SetEnvIfNotExists("DD_TRACE_HTTPMESSAGEHANDLER_ENABLED", "true");
            SetEnvIfNotExists("DD_TRACE_NPGSQL_ENABLED", "true");
            SetEnvIfNotExists("DD_TRACE_STACKEXCHANGEREDIS_ENABLED", "true");
            SetEnvIfNotExists("DD_TRACE_ADONET_ENABLED", "true");
        }

        private static void SetEnvIfNotExists(string key, string value)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        /// <summary>
        /// Configures Serilog with Datadog log shipping and trace correlation.
        /// </summary>
        /// <param name="builder">The host builder</param>
        /// <param name="serviceName">The service name for log tagging</param>
        /// <param name="configuration">The application configuration</param>
        /// <returns>The host builder for chaining</returns>
        public static IHostBuilder UseDatadogLogging(this IHostBuilder builder, string serviceName, IConfiguration configuration)
        {
            var (apiKey, environment, version) = GetDatadogConfig(serviceName, configuration);

            builder.UseSerilog((context, loggerConfiguration) =>
            {
                ConfigureSerilog(loggerConfiguration, serviceName, apiKey, environment, version);
            });

            return builder;
        }

        /// <summary>
        /// Configures Serilog with Datadog log shipping for HostApplicationBuilder (used in worker services).
        /// </summary>
        /// <param name="builder">The host application builder</param>
        /// <param name="serviceName">The service name for log tagging</param>
        /// <returns>The host application builder for chaining</returns>
        public static HostApplicationBuilder UseDatadogLogging(this HostApplicationBuilder builder, string serviceName)
        {
            var (apiKey, environment, version) = GetDatadogConfig(serviceName, builder.Configuration);

            builder.Services.AddSerilog((services, loggerConfiguration) =>
            {
                ConfigureSerilog(loggerConfiguration, serviceName, apiKey, environment, version);
            });

            return builder;
        }

        private static (string apiKey, string environment, string version) GetDatadogConfig(string serviceName, IConfiguration configuration)
        {
            var datadogSection = configuration.GetSection("Datadog");
            var apiKey = datadogSection["LogsApiKey"] ?? Environment.GetEnvironmentVariable("DD_API_KEY") ?? string.Empty;
            var environment = datadogSection["Environment"] ?? Environment.GetEnvironmentVariable("DD_ENV") ?? "development";
            var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";
            return (apiKey, environment, version);
        }

        private static void ConfigureSerilog(LoggerConfiguration loggerConfiguration, string serviceName, string apiKey, string environment, string version)
        {
            loggerConfiguration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.WithProperty("dd.service", serviceName)
                .Enrich.WithProperty("dd.env", environment)
                .Enrich.WithProperty("dd.version", version)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

            // Only add Datadog sink if API key is configured
            if (!string.IsNullOrEmpty(apiKey))
            {
                loggerConfiguration.WriteTo.DatadogLogs(
                    apiKey: apiKey,
                    service: serviceName,
                    host: Environment.MachineName,
                    tags: new[] { $"env:{environment}", $"version:{version}" },
                    configuration: new DatadogConfiguration
                    {
                        Url = "https://http-intake.logs.datadoghq.eu" // EU region, change to .com for US
                    });
            }
        }

        /// <summary>
        /// Configures both Datadog tracing and logging in one call.
        /// </summary>
        /// <param name="builder">The host builder</param>
        /// <param name="serviceName">The service name</param>
        /// <param name="configuration">The application configuration</param>
        /// <returns>The host builder for chaining</returns>
        public static IHostBuilder UseDatadog(this IHostBuilder builder, string serviceName, IConfiguration configuration)
        {
            return builder
                .UseDatadogTracing(serviceName)
                .UseDatadogLogging(serviceName, configuration);
        }

        /// <summary>
        /// Configures both Datadog tracing and logging for HostApplicationBuilder (used in worker services).
        /// </summary>
        /// <param name="builder">The host application builder</param>
        /// <param name="serviceName">The service name</param>
        /// <returns>The host application builder for chaining</returns>
        public static HostApplicationBuilder UseDatadog(this HostApplicationBuilder builder, string serviceName)
        {
            return builder
                .UseDatadogTracing(serviceName)
                .UseDatadogLogging(serviceName);
        }
    }
}
