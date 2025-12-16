using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;
using TorreClou.API.Extensions;
using TorreClou.API.Filters;
using TorreClou.Application.Extensions;
using TorreClou.Infrastructure.Extensions;
using TorreClou.Infrastructure.Services;
using Hangfire;
using Hangfire.PostgreSql;
using OpenTelemetry.Metrics;

const string ServiceName = "torreclou-api";

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting web application");

    var builder = WebApplication.CreateBuilder(args);

    // Add CORS for development
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });
    }

    // Configure Serilog with Loki
    var lokiUrl = builder.Configuration["Observability:LokiUrl"] ?? 
                  Environment.GetEnvironmentVariable("LOKI_URL") ?? 
                  "http://localhost:3100";
    var lokiUsername = builder.Configuration["Observability:LokiUsername"] ?? 
                       Environment.GetEnvironmentVariable("LOKI_USERNAME");
    var lokiApiKey = builder.Configuration["Observability:LokiApiKey"] ?? 
                     Environment.GetEnvironmentVariable("LOKI_API_KEY");
    
    builder.Host.UseSerilog((context, loggerConfiguration) =>
    {
        loggerConfiguration
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("service", ServiceName)
            .Enrich.WithProperty("environment", builder.Environment.EnvironmentName)
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

        // Add Loki sink if URL is configured
        if (!string.IsNullOrEmpty(lokiUrl) && !lokiUrl.Equals("http://localhost:3100", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(lokiUsername) && !string.IsNullOrEmpty(lokiApiKey))
            {
                var credentials = new LokiCredentials
                {
                    Login = lokiUsername,
                    Password = lokiApiKey
                };
                loggerConfiguration.WriteTo.GrafanaLoki(lokiUrl, credentials: credentials);
            }
            else
            {
                loggerConfiguration.WriteTo.GrafanaLoki(lokiUrl);
            }
        }
    });

    // Add OpenTelemetry
    builder.Services.AddTorreClouOpenTelemetry(ServiceName, builder.Configuration, builder.Environment, includeAspNetCoreInstrumentation: true);

    // Add services to the container.
    builder.Services.AddInfrastructureServices(builder.Configuration);
    builder.Services.AddApplicationServices();
    builder.Services.AddApiServices(builder.Configuration);
    builder.Services.AddIdentityServices(builder.Configuration);

    // Hangfire Dashboard configuration (read-only, no server)
    var hangfireConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddHangfire((provider, config) => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(hangfireConnectionString))
    );

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }
    if (!builder.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseExceptionHandler();
    app.UseHttpsRedirection();

    // Use CORS before authentication
    if (app.Environment.IsDevelopment())
    {
        app.UseCors();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    // Hangfire Dashboard (add authorization if needed)
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireAuthorizationFilter() },
        DashboardTitle = "TorreClou Jobs Dashboard",
        StatsPollingInterval = 2000
    });

    // Prometheus metrics endpoint
    app.UseOpenTelemetryPrometheusScrapingEndpoint();

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

