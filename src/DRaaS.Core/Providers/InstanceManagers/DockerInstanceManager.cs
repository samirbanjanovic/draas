using DRaaS.Core.Models;
using DRaaS.Core.Services.Storage;
using DRaaS.Core.Services.ResourceAllocation;

namespace DRaaS.Core.Providers.InstanceManagers;

public class DockerInstanceManager : IDrasiServerInstanceManager
{
    private readonly IInstanceRuntimeStore _runtimeStore;

    public DockerInstanceManager(IInstanceRuntimeStore runtimeStore)
    {
        _runtimeStore = runtimeStore;
    }

    public string PlatformType => "Docker";

    public async Task<InstanceRuntimeInfo> StartInstanceAsync(string instanceId, Configuration configuration)
    {
        // TODO: Implement Docker container creation and startup
        // 1. Build Docker run command with configuration
        // 2. Map ports (configuration.Port)
        // 3. Set environment variables (HOST, LOG_LEVEL)
        // 4. Mount configuration file as volume
        // 5. Execute docker run command
        // 6. Capture container ID

        var runtimeInfo = new InstanceRuntimeInfo
        {
            InstanceId = instanceId,
            Status = InstanceStatus.Running,
            StartedAt = DateTime.UtcNow,
            ContainerId = $"docker-{Guid.NewGuid():N}",
            RuntimeMetadata = new Dictionary<string, string>
            {
                ["PlatformType"] = PlatformType,
                ["Image"] = "drasi/server:latest",
                ["Port"] = configuration.Port?.ToString() ?? "8080",
                ["Host"] = configuration.Host ?? "0.0.0.0"
            }
        };

        await _runtimeStore.SaveAsync(runtimeInfo);
        return runtimeInfo;
    }

    public async Task<InstanceRuntimeInfo> StopInstanceAsync(string instanceId)
    {
        // TODO: Implement Docker container stop
        // 1. Get container ID from runtime info
        // 2. Execute docker stop command
        // 3. Optionally docker rm to clean up

        var runtimeInfo = await _runtimeStore.GetAsync(instanceId);
        if (runtimeInfo == null)
        {
            throw new KeyNotFoundException($"Instance '{instanceId}' not found or not running");
        }

        var stoppedInfo = runtimeInfo with
        {
            Status = InstanceStatus.Stopped,
            StoppedAt = DateTime.UtcNow
        };

        await _runtimeStore.SaveAsync(stoppedInfo);
        return stoppedInfo;
    }

    public async Task<InstanceRuntimeInfo> RestartInstanceAsync(string instanceId)
    {
        // TODO: Implement Docker container restart
        // Option 1: docker restart <container-id>
        // Option 2: Stop then Start

        await StopInstanceAsync(instanceId);

        var info = await _runtimeStore.GetAsync(instanceId);
        if (info == null)
        {
            throw new KeyNotFoundException($"Instance '{instanceId}' not found");
        }

        // Need to get configuration to restart - this should be passed or retrieved
        throw new NotImplementedException("RestartInstanceAsync requires configuration retrieval");
    }

    public async Task<InstanceRuntimeInfo> GetInstanceStatusAsync(string instanceId)
    {
        // TODO: Implement Docker container status check
        // 1. Execute docker ps -a --filter id=<container-id>
        // 2. Parse status (running, stopped, exited, etc.)
        // 3. Update runtime info

        var runtimeInfo = await _runtimeStore.GetAsync(instanceId);
        if (runtimeInfo == null)
        {
            throw new KeyNotFoundException($"Instance '{instanceId}' not found");
        }

        return runtimeInfo;
    }

    public async Task<IEnumerable<InstanceRuntimeInfo>> GetAllInstanceStatusesAsync()
    {
        // TODO: Query all Docker containers with drasi label/tag
        return await _runtimeStore.GetByPlatformAsync(Models.PlatformType.Docker);
    }

    public Task<bool> IsAvailableAsync()
    {
        // TODO: Check if Docker is installed and running
        // Execute: docker --version or docker info
        return Task.FromResult(true); // Stub: assume available
    }

    public Task<ServerConfiguration> AllocateResourcesAsync(IPortAllocator portAllocator)
    {
        // Docker containers can bind to 0.0.0.0 and use port mapping
        var port = portAllocator.AllocatePort();

        var config = new ServerConfiguration
        {
            Host = "0.0.0.0", // Docker allows binding to all interfaces
            Port = port,
            LogLevel = "info"
        };

        return Task.FromResult(config);
    }
}
