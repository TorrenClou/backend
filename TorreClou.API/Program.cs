using Hangfire;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using TorreClou.API.Extensions;
using TorreClou.API.Filters;
using TorreClou.Application.Extensions;
using TorreClou.Infrastructure.Extensions;
using TorreClou.Infrastructure.Services;

const string ServiceName = "torreclou-api";

// Bootstrap logger for startup errors only
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog (replaces bootstrap logger)
    builder.Configuration.ConfigureSharedSerilog(ServiceName, builder.Environment.EnvironmentName);
    builder.Host.UseSerilog();

    Log.Information("Starting {ServiceName}", ServiceName);

    // Infrastructure
    builder.Services.AddSharedDatabase(builder.Configuration);
    builder.Services.AddSharedRedis(builder.Configuration);
    builder.Services.AddTorreClouOpenTelemetry(ServiceName, builder.Configuration, builder.Environment, true);
    builder.Services.AddSharedHangfireBase(builder.Configuration);

    // Application Services
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);
    builder.Services.AddApiServices(builder.Configuration);
    builder.Services.AddIdentityServices(builder.Configuration);
    builder.Services.AddHttpClient();
    builder.Services.AddHealthChecks();

    // Sweeps Hangfire records left Processing by workers that were killed rather than
    // shut down. Registered here alone: the API is always up and single-instance, so
    // the sweep does not run three times over from each worker.
    builder.Services.AddHostedService<TorreClou.Infrastructure.Services.HangfireOrphanReaper>();

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    var app = builder.Build();

    // Conditionally apply database migrations at startup (gated by config flag)
    var applyMigrations = app.Configuration.GetValue<bool>("APPLY_MIGRATIONS");

    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();

        if (!applyMigrations)
        {
            logger.LogInformation("Database migrations skipped (APPLY_MIGRATIONS=false)");
        }
        else
        {
            try
            {
                var context = services.GetRequiredService<TorreClou.Infrastructure.Data.ApplicationDbContext>();
                logger.LogInformation("Acquiring advisory lock for database migration...");

                // Use PostgreSQL advisory lock to prevent concurrent migration attempts
                const int advisoryLockId = 839_275_194;

                using var connection = context.Database.GetDbConnection();
                await connection.OpenAsync();

                using var lockCommand = connection.CreateCommand();
                lockCommand.CommandText = $"SELECT pg_advisory_lock({advisoryLockId})";
                await lockCommand.ExecuteNonQueryAsync();

                try
                {
                    logger.LogInformation("Advisory lock acquired. Checking for pending database migrations...");
                    await context.Database.MigrateAsync();
                    logger.LogInformation("Database migrations applied successfully");
                }
                finally
                {
                    using var unlockCommand = connection.CreateCommand();
                    unlockCommand.CommandText = $"SELECT pg_advisory_unlock({advisoryLockId})";
                    await unlockCommand.ExecuteNonQueryAsync();
                    logger.LogInformation("Advisory lock released");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while migrating the database");
                throw;
            }
        }
    }

    // Middleware
    app.UseExceptionHandler();
    app.UseCors("AllowAll");

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }
    else if (app.Configuration.GetValue<bool>("Api:UseHttpsRedirection"))
    {
        // Off by default: in every deployment of this stack the container listens on
        // plain HTTP:8080 and TLS is terminated upstream. Enabling it without an HTTPS
        // port makes ASP.NET log "Failed to determine the https port for redirect" on
        // every request, including each Prometheus scrape.
        app.UseHttpsRedirection();
        app.UseHsts();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireAuthorizationFilter()],
        DashboardTitle = "TorreClou Jobs"
    });

    // Only when the exporter was actually registered. Mapping the scraping endpoint
    // without it throws at startup, which turned "Prometheus off" — now a setting a user
    // can toggle — into a container that will not boot.
    if (builder.Configuration.GetSection("Observability").GetValue("EnablePrometheus", true))
    {
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
    }
    app.MapHealthChecks("/health").AllowAnonymous();
    app.MapControllers();

    Log.Information("{ServiceName} started successfully", ServiceName);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}
