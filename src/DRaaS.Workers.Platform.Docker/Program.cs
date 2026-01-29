using DRaaS.Workers.Platform.Docker;
using DRaaS.Core.Messaging;
using DRaaS.Core.Providers;
using DRaaS.Core.Services.Storage;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

// Configure Redis connection
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    return ConnectionMultiplexer.Connect(redisConnectionString);
});

// Configure message bus
builder.Services.AddSingleton<IMessageBus, RedisMessageBus>();

// Register instance manager
builder.Services.AddSingleton<DockerInstanceManager>();
builder.Services.AddSingleton<IDrasiServerInstanceManager>(sp => sp.GetRequiredService<DockerInstanceManager>());

// Configure runtime store
builder.Services.AddSingleton<IInstanceRuntimeStore, InMemoryInstanceRuntimeStore>();

// Register background workers
builder.Services.AddHostedService<DockerCommandWorker>();
builder.Services.AddHostedService<DockerMonitorWorker>();

var host = builder.Build();

await host.RunAsync();
