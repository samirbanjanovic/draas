using DRaaS.Core.Models;
using DRaaS.Core.Services.ResourceAllocation;

namespace DRaaS.Core.Providers;

public interface IDrasiServerInstanceManager
{
    /// <summary>
    /// Gets the platform type this manager supports (Docker, AKS, Process, etc.)
    /// </summary>
    string PlatformType { get; }

    /// <summary>
    /// Starts a Drasi server instance using the platform-specific runtime
    /// </summary>
    /// <param name="instanceId">The unique identifier for the instance</param>
    /// <param name="configuration">The configuration for the instance</param>
    /// <returns>Runtime information about the started instance</returns>
    Task<InstanceRuntimeInfo> StartInstanceAsync(string instanceId, Configuration configuration);

    /// <summary>
    /// Stops a running Drasi server instance
    /// </summary>
    /// <param name="instanceId">The unique identifier for the instance</param>
    /// <returns>Runtime information about the stopped instance</returns>
    Task<InstanceRuntimeInfo> StopInstanceAsync(string instanceId);

    /// <summary>
    /// Restarts a Drasi server instance
    /// </summary>
    /// <param name="instanceId">The unique identifier for the instance</param>
    /// <returns>Runtime information about the restarted instance</returns>
    Task<InstanceRuntimeInfo> RestartInstanceAsync(string instanceId);

    /// <summary>
    /// Gets the current runtime status of a Drasi server instance
    /// </summary>
    /// <param name="instanceId">The unique identifier for the instance</param>
    /// <returns>Runtime information about the instance</returns>
    Task<InstanceRuntimeInfo> GetInstanceStatusAsync(string instanceId);

    /// <summary>
    /// Gets runtime information for all instances managed by this platform
    /// </summary>
    /// <returns>Collection of runtime information for all instances</returns>
    Task<IEnumerable<InstanceRuntimeInfo>> GetAllInstanceStatusesAsync();

    /// <summary>
    /// Checks if the instance manager is available and properly configured
    /// </summary>
    /// <returns>True if the manager is ready to manage instances</returns>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Allocates platform-specific resources (host, port, etc.) for a new instance.
    /// The manager determines what resources it needs and requests shared resources (like ports) from the allocator.
    /// </summary>
    /// <param name="portAllocator">Shared port allocator to request available ports</param>
    /// <returns>Server configuration with allocated resources</returns>
    Task<ServerConfiguration> AllocateResourcesAsync(IPortAllocator portAllocator);
}
