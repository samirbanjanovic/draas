using System.Collections.Concurrent;
using DRaaS.ControlPlane.Models;

namespace DRaaS.ControlPlane.Services;

public class PlatformOrchestratorService : IPlatformOrchestratorService
{
    private readonly PlatformType _defaultPlatform;
    private readonly ConcurrentDictionary<int, bool> _allocatedPorts = new();
    private int _nextPort = 8080;
    private readonly object _portLock = new();

    public PlatformOrchestratorService(PlatformType? defaultPlatform = null)
    {
        _defaultPlatform = defaultPlatform ?? PlatformType.Process;
    }

    public Task<PlatformType> SelectPlatformAsync()
    {
        // TODO: Implement platform selection strategy
        // Options:
        // 1. Round-robin across available platforms
        // 2. Load-based (check CPU/memory on each platform)
        // 3. Cost-based (prefer cheaper platforms first)
        // 4. Capability-based (some workloads require specific platforms)
        
        // For now, return configured default platform
        return Task.FromResult(_defaultPlatform);
    }

    public Task<ServerConfiguration> AllocateResourcesAsync(PlatformType platformType)
    {
        // TODO: Implement resource allocation based on platform type
        var config = platformType switch
        {
            PlatformType.Process => AllocateProcessResources(),
            PlatformType.Docker => AllocateDockerResources(),
            PlatformType.AKS => AllocateAksResources(),
            _ => throw new NotSupportedException($"Platform type {platformType} is not supported")
        };

        return Task.FromResult(config);
    }

    public Task ReleaseResourcesAsync(string instanceId, ServerConfiguration configuration)
    {
        // TODO: Implement resource release
        // Release allocated port
        if (configuration.Port.HasValue)
        {
            _allocatedPorts.TryRemove(configuration.Port.Value, out _);
        }

        return Task.CompletedTask;
    }

    public PlatformType GetDefaultPlatform()
    {
        return _defaultPlatform;
    }

    private ServerConfiguration AllocateProcessResources()
    {
        // For bare metal processes, allocate sequential ports on localhost
        var port = AllocatePort();
        return new ServerConfiguration
        {
            Host = "127.0.0.1",
            Port = port,
            LogLevel = "info"
        };
    }

    private ServerConfiguration AllocateDockerResources()
    {
        // For Docker, we can bind to 0.0.0.0 and let Docker handle port mapping
        var port = AllocatePort();
        return new ServerConfiguration
        {
            Host = "0.0.0.0",
            Port = port,
            LogLevel = "info"
        };
    }

    private ServerConfiguration AllocateAksResources()
    {
        // For AKS, each pod gets its own IP, use standard port
        // The Kubernetes service will handle load balancing
        return new ServerConfiguration
        {
            Host = "0.0.0.0",
            Port = 8080, // Standard port, K8s service handles routing
            LogLevel = "info"
        };
    }

    private int AllocatePort()
    {
        lock (_portLock)
        {
            // Simple round-robin port allocation
            // TODO: Check if port is actually available on the system
            while (_allocatedPorts.ContainsKey(_nextPort))
            {
                _nextPort++;
                if (_nextPort > 9000) // Arbitrary upper limit
                {
                    _nextPort = 8080; // Wrap around
                }
            }

            var allocatedPort = _nextPort;
            _allocatedPorts.TryAdd(allocatedPort, true);
            _nextPort++;

            return allocatedPort;
        }
    }
}
