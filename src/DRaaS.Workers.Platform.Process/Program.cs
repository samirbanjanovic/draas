using DRaaS.Workers.Platform.Process;
using DRaaS.Core.Messaging;
using DRaaS.Core.Providers;
using DRaaS.Core.Services.Storage;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

// Configure Redis connection
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
var redisConnection = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
builder.Services.AddSingleton<IConnectionMultiplexer>(redisConnection);

// Configure message bus
builder.Services.AddSingleton<IMessageBus, RedisMessageBus>();

// Configure process instance manager options
builder.Services.Configure<ProcessInstanceManagerOptions>(
    builder.Configuration.GetSection("ProcessInstanceManager"));

// Register instance manager as both its concrete type and interface
builder.Services.AddSingleton<ProcessInstanceManager>();
builder.Services.AddSingleton<IDrasiServerInstanceManager>(sp => sp.GetRequiredService<ProcessInstanceManager>());

// Configure runtime store
builder.Services.AddSingleton<IInstanceRuntimeStore, InMemoryInstanceRuntimeStore>();

// Register background workers
builder.Services.AddHostedService<ProcessCommandWorker>();
builder.Services.AddHostedService<ProcessMonitorWorker>();

var host = builder.Build();

await host.RunAsync();
