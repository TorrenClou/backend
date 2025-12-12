using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;
using Serilog;
using TorreClou.API.Extensions;
using TorreClou.API.Filters;
using TorreClou.Application.Extensions;
using TorreClou.Infrastructure.Extensions;
using Hangfire;
using Hangfire.PostgreSql;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting web application");

    var builder = WebApplication.CreateBuilder(args);

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

    app.UseAuthentication();
    app.UseAuthorization();

    // Hangfire Dashboard (add authorization if needed)
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireAuthorizationFilter() },
        DashboardTitle = "TorreClou Jobs Dashboard",
        StatsPollingInterval = 2000
    });

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

