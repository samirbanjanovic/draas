using DRaaS.Core.Models;
using DRaaS.Core.Providers;
using DRaaS.Core.Services.ResourceAllocation;
using DRaaS.Core.Services.Factory;

namespace DRaaS.Core.Services.Orchestration;

public class PlatformOrchestratorService : IPlatformOrchestratorService
{
    private readonly PlatformType _defaultPlatform;
    private readonly IPortAllocator _portAllocator;
    private readonly IInstanceManagerFactory _managerFactory;

    public PlatformOrchestratorService(
        IPortAllocator portAllocator,
        IInstanceManagerFactory managerFactory,
        PlatformType? defaultPlatform = null)
    {
        _portAllocator = portAllocator;
        _managerFactory = managerFactory;
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

    public async Task<ServerConfiguration> AllocateResourcesAsync(PlatformType platformType)
    {
        // Delegate to the platform-specific manager to determine resource requirements
        var manager = _managerFactory.GetManager(platformType);
        if (manager == null)
        {
            throw new NotSupportedException($"Platform type {platformType} is not supported or no manager is registered");
        }

        // Manager determines its resource needs and requests ports from shared allocator
        var configuration = await manager.AllocateResourcesAsync(_portAllocator);
        return configuration;
    }

    public Task ReleaseResourcesAsync(string instanceId, ServerConfiguration configuration)
    {
        // Release allocated port back to the pool
        if (configuration.Port.HasValue)
        {
            _portAllocator.ReleasePort(configuration.Port.Value);
        }

        return Task.CompletedTask;
    }

    public PlatformType GetDefaultPlatform()
    {
        return _defaultPlatform;
    }
}
