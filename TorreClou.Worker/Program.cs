using StackExchange.Redis;
using TorreClou.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Redis configuration
var redisConnectionString = builder.Configuration.GetSection("Redis:ConnectionString").Value ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnectionString)
);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
