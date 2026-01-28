using DRaaS.Core.Providers;
using DRaaS.Core.Providers.InstanceManagers;
using DRaaS.Core.Services.Instance;
using DRaaS.Core.Services.Storage;
using DRaaS.Core.Services.ResourceAllocation;
using DRaaS.Core.Services.Monitoring;
using DRaaS.Core.Services.Orchestration;
using DRaaS.Core.Services.Factory;
using DRaaS.Core.Models;
using Scalar.AspNetCore;
using YamlDotNet.Helpers;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DRaaS.ControlPlane;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllers()
            .AddNewtonsoftJson(); // Required for JsonPatch support


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

        // Register platform orchestrator (configure default platform here)
        // Port allocator is shared across all managers via the orchestrator
        builder.Services.AddSingleton<IPortAllocator, PortAllocator>();

        // Register shared runtime store (singleton so all managers share the same store)
        builder.Services.AddSingleton<IInstanceRuntimeStore, InMemoryInstanceRuntimeStore>();

        // Register instance service
        builder.Services.AddSingleton<IDrasiInstanceService, DrasiInstanceService>();

        // Register instance managers (they all inject the shared runtime store)
        builder.Services.AddSingleton<IDrasiServerInstanceManager, DockerInstanceManager>();
        builder.Services.AddSingleton<IDrasiServerInstanceManager, AksInstanceManager>();
        builder.Services.AddSingleton<IDrasiServerInstanceManager, ProcessInstanceManager>();

        // Register instance manager factory
        builder.Services.AddSingleton<IInstanceManagerFactory>(sp =>
        {
            var managers = sp.GetServices<IDrasiServerInstanceManager>();
            return new InstanceManagerFactory(managers, PlatformType.Process); // Default to Process
        });

        // Register orchestrator (depends on port allocator and manager factory)
        builder.Services.AddSingleton<IPlatformOrchestratorService>(sp =>
        {
            var portAllocator = sp.GetRequiredService<IPortAllocator>();
            var managerFactory = sp.GetRequiredService<IInstanceManagerFactory>();
            return new PlatformOrchestratorService(portAllocator, managerFactory, PlatformType.Process);
        });

        // Register configuration provider
        builder.Services.AddSingleton<IDrasiServerConfigurationProvider, DrasiServerConfigurationProvider>();

        // Register status update service (centralized status bus)
        builder.Services.AddSingleton<IStatusUpdateService, StatusUpdateService>();

        // Register process status monitor (polls local processes)
        builder.Services.AddSingleton<IStatusMonitor>(sp =>
        {
            var runtimeStore = sp.GetRequiredService<IInstanceRuntimeStore>();
            var statusUpdateService = sp.GetRequiredService<IStatusUpdateService>();

            // Get the ProcessInstanceManager to access its tracked processes
            var processManager = sp.GetServices<IDrasiServerInstanceManager>()
                .OfType<ProcessInstanceManager>()
                .FirstOrDefault();

            if (processManager == null)
            {
                throw new InvalidOperationException("ProcessInstanceManager not registered");
            }

            return new ProcessStatusMonitor(
                runtimeStore,
                statusUpdateService,
                processManager.TrackedProcesses);
        });

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();

        // Start status monitoring for polling-based platforms (Process)
        var statusMonitor = app.Services.GetRequiredService<IStatusMonitor>();
        if (statusMonitor.RequiresPolling)
        {
            var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
            _ = statusMonitor.StartMonitoringAsync(lifetime.ApplicationStopping);
        }

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
