using DRaaS.CoreLib.Models;

namespace DRaaS.CoreLib.Providers;

/// <summary>
/// Platform-agnostic interface for managing instance runtimes.
/// Implementations manage infrastructure (processes, containers, pods) without domain logic.
/// </summary>
public interface IPlatformInstanceProvider 
{
    /// <summary>
    /// Platform type identifier (e.g., "Process", "Docker", "Kubernetes")
    /// </summary>
    string PlatformType { get; }

    /// <summary>
    /// Indicates if this provider is available and properly configured
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Deploys an instance (creates runtime environment without starting)
    /// </summary>
    Task<PlacementProviderRuntimeInfo> DeployInstanceAsync(
        string instanceId,
        string instanceName,
        DrasiConfiguration configuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a deployed instance
    /// </summary>
    Task<PlacementProviderRuntimeInfo> StartInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a running instance gracefully
    /// </summary>
    Task<PlacementProviderRuntimeInfo> StopInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the instance runtime and cleans up resources
    /// </summary>
    Task DeleteInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets runtime information for a specific instance
    /// </summary>
    Task<PlacementProviderRuntimeInfo> GetInstanceInfoAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all instances managed by this provider
    /// </summary>
    Task<IEnumerable<PlacementProviderRuntimeInfo>> GetAllInstancesAsync(CancellationToken cancellationToken = default);
}
