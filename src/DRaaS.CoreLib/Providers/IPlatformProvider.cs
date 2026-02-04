using DRaaS.CoreLib.Models;

namespace DRaaS.CoreLib.Providers;

public interface IPlatformProvider 
{
    string PlatformType { get; }
    bool IsAvailable { get; }

    PlatformInfo GetPlatformInfo();

    Task<PlacementProviderRuntimeInfo> DeployInstanceAsync(
        string instanceId,
        string instanceName,
        DrasiConfiguration configuration,
        CancellationToken cancellationToken = default);

    Task<PlacementProviderRuntimeInfo> StartInstanceAsync(string instanceId, CancellationToken cancellationToken = default);
    Task<PlacementProviderRuntimeInfo> StopInstanceAsync(string instanceId, CancellationToken cancellationToken = default);
    Task DeleteInstanceAsync(string instanceId, CancellationToken cancellationToken = default);
    Task<PlacementProviderRuntimeInfo> GetInstanceInfoAsync(string instanceId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PlacementProviderRuntimeInfo>> GetAllInstancesAsync(CancellationToken cancellationToken = default);
}
