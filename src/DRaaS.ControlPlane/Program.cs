using DRaaS.ControlPlane.Providers;
using DRaaS.ControlPlane.Providers.InstanceManagers;
using DRaaS.ControlPlane.Services;
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
        builder.Services.AddSingleton<IPlatformOrchestratorService>(sp => 
            new PlatformOrchestratorService(PlatformType.Process)); // Can be configured from appsettings

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

        // Register configuration provider
        builder.Services.AddSingleton<IDrasiServerConfigurationProvider, DrasiServerConfigurationProvider>();

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
