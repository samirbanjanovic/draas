using DRaaS.CoreLib.Clients.Docker;
using DRaaS.CoreLib.Providers;
using DRaaS.CoreLib.Providers.Abstractions;
using DRaaS.CoreLib.Providers.Impl;
using DRaaS.CoreLib.Services;
using DRaaS.CoreLib.Services.Impl;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace DRaaS.CoreLib.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddDraas(this IServiceCollection services)
    {
        services.AddSingleton<IInstanceOrchestrationService, InstanceOrchestrationService>()
                .AddSingleton<IPlatformProviderRegistry, PlatformProviderRegistry>();

        return services;
    }

    public static IServiceCollection AddInMemoryInstanceStore(this IServiceCollection services)
        => services.AddSingleton<IDrasiInstanceStorageService, LocalDrasiInstanceStorageService>();

    public static IServiceCollection AddDockerPlatform(
        this IServiceCollection services,
        Action<DockerInstanceProviderOptions>? options = null,
        string? dockerEndpoint = null)
    {
        if(options != null)
        {
            services.Configure(options);
        }

        services.AddSingleton<IDraasDockerClient>(sp =>
        {
            var endpoint = dockerEndpoint ?? GetDefaultDockerEndpoint();
            return new DockerSdkClient(endpoint);
        });

        services.AddSingleton<IPlatformProvider, DockerPlatformProvider>();

        return services;
    }

    public static IServiceCollection AddBaremetalPlatform(
        this IServiceCollection services,
        Action<BaremetalProviderOptions>? configureOptions = null)
    {
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        services.AddSingleton<IPlatformProvider, BaremetalPlatformProvider>();        

        return services;
    }


    private static string GetDefaultDockerEndpoint()
    {
        if (OperatingSystem.IsWindows())
            return "npipe://./pipe/docker_engine";
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            return "unix:///var/run/docker.sock";
        else
            return "tcp://localhost:2375";
    }
}
