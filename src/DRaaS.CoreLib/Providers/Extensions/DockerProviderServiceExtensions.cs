using DRaaS.CoreLib.Clients.Docker;
using DRaaS.CoreLib.Providers.Abstractions;
using DRaaS.CoreLib.Providers.Impl;
using Microsoft.Extensions.DependencyInjection;

namespace DRaaS.CoreLib.Providers.Extensions;

/// <summary>
/// Extension methods for registering Docker provider services.
/// </summary>
public static class DockerProviderServiceExtensions
{
    /// <summary>
    /// Registers Docker-based instance provider with Docker.DotNet SDK client.
    /// </summary>
    /// <param name="dockerEndpoint">
    /// Docker daemon endpoint. 
    /// Windows: "npipe://./pipe/docker_engine"
    /// Linux: "unix:///var/run/docker.sock"
    /// TCP: "tcp://localhost:2375"
    /// </param>
    public static IServiceCollection AddDockerInstanceProvider(
        this IServiceCollection services,
        string? dockerEndpoint = null)
    {
        // Register Docker SDK client
        services.AddSingleton<IDraasDockerClient>(sp =>
        {
            var endpoint = dockerEndpoint ?? GetDefaultDockerEndpoint();
            return new DockerSdkClient(endpoint);
        });

        // Register Docker instance provider
        services.AddSingleton<DockerInstanceProvider>();

        return services;
    }

    private static string GetDefaultDockerEndpoint()
    {
        if (OperatingSystem.IsWindows())
        {
            return "npipe://./pipe/docker_engine";
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return "unix:///var/run/docker.sock";
        }
        else
        {
            // Fallback to TCP (least secure, but works across platforms)
            return "tcp://localhost:2375";
        }
    }
}
