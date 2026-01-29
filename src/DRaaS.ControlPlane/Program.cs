using DRaaS.Core.Providers;
using DRaaS.Core.Services.Instance;
using DRaaS.Core.Services.Storage;
using DRaaS.Core.Services.Monitoring;
using DRaaS.Core.Messaging;
using Scalar.AspNetCore;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using StackExchange.Redis;

namespace DRaaS.ControlPlane;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllers()
            .AddNewtonsoftJson(); // Required for JsonPatch support

        // Configure Redis connection for message bus
        var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            return ConnectionMultiplexer.Connect(redisConnectionString);
        });

        // Configure message bus (core messaging infrastructure)
        builder.Services.AddSingleton<IMessageBus, RedisMessageBus>();

        // Configure YAML serializers for configuration management
        builder.Services.AddSingleton<IDeserializer>(sp =>
            new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .WithEnumNamingConvention(CamelCaseNamingConvention.Instance)
                .Build());

        builder.Services.AddSingleton(sp =>
            new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .WithEnumNamingConvention(CamelCaseNamingConvention.Instance)
                .Build());

        // Register shared runtime store for instance state
        builder.Services.AddSingleton<IInstanceRuntimeStore, InMemoryInstanceRuntimeStore>();

        // Register instance service for managing instance metadata
        builder.Services.AddSingleton<IDrasiInstanceService, DrasiInstanceService>();

        // Register configuration provider for instance configurations
        builder.Services.AddSingleton<IDrasiServerConfigurationProvider, DrasiServerConfigurationProvider>();

        // Register status update service for status change notifications
        builder.Services.AddSingleton<IStatusUpdateService, StatusUpdateService>();

        // Register event subscription service to receive events from workers
        builder.Services.AddHostedService<DRaaS.ControlPlane.Services.EventSubscriptionService>();

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.MapControllers();

        app.Run();
    }
}
