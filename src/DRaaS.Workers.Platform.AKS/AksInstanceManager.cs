using DRaaS.Core.Models;
using DRaaS.Core.Providers;
using DRaaS.Core.Services.Storage;
using DRaaS.Core.Services.ResourceAllocation;

namespace DRaaS.Workers.Platform.AKS;

public class AksInstanceManager : IDrasiServerInstanceManager
{
    private readonly IInstanceRuntimeStore _runtimeStore;

    public AksInstanceManager(IInstanceRuntimeStore runtimeStore)
    {
        _runtimeStore = runtimeStore;
    }

    public string PlatformType => "AKS";

    public async Task<InstanceRuntimeInfo> StartInstanceAsync(string instanceId, Configuration configuration)
    {
        // TODO: Implement Kubernetes deployment creation
        var runtimeInfo = new InstanceRuntimeInfo
        {
            InstanceId = instanceId,
            Status = InstanceStatus.Running,
            StartedAt = DateTime.UtcNow,
            PodName = $"drasi-{instanceId.ToLower()}",
            Namespace = "drasi-instances",
            RuntimeMetadata = new Dictionary<string, string>
            {
                ["PlatformType"] = PlatformType,
                ["ClusterName"] = "drasi-aks-cluster",
                ["DeploymentName"] = $"drasi-{instanceId.ToLower()}",
                ["ServiceName"] = $"drasi-svc-{instanceId.ToLower()}",
                ["Port"] = configuration.Port?.ToString() ?? "8080"
            }
        };

        await _runtimeStore.SaveAsync(runtimeInfo);
        return runtimeInfo;
    }

    public async Task<InstanceRuntimeInfo> StopInstanceAsync(string instanceId)
    {
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
        var info = await _runtimeStore.GetAsync(instanceId);
        if (info == null)
        {
            throw new KeyNotFoundException($"Instance '{instanceId}' not found");
        }

        var restartedInfo = info with
        {
            StartedAt = DateTime.UtcNow,
            RuntimeMetadata = new Dictionary<string, string>(info.RuntimeMetadata)
            {
                ["LastRestart"] = DateTime.UtcNow.ToString("o")
            }
        };

        await _runtimeStore.SaveAsync(restartedInfo);
        return restartedInfo;
    }

    public async Task<InstanceRuntimeInfo> GetInstanceStatusAsync(string instanceId)
    {
        var runtimeInfo = await _runtimeStore.GetAsync(instanceId);
        if (runtimeInfo == null)
        {
            throw new KeyNotFoundException($"Instance '{instanceId}' not found");
        }

        return runtimeInfo;
    }

    public async Task<IEnumerable<InstanceRuntimeInfo>> GetAllInstanceStatusesAsync()
    {
        return await _runtimeStore.GetByPlatformAsync(DRaaS.Core.Models.PlatformType.AKS);
    }

    public Task<bool> IsAvailableAsync()
    {
        return Task.FromResult(true);
    }

    public Task<ServerConfiguration> AllocateResourcesAsync(IPortAllocator portAllocator)
    {
        var config = new ServerConfiguration
        {
            Host = "0.0.0.0",
            Port = 8080,
            LogLevel = "info"
        };

        return Task.FromResult(config);
    }
}
