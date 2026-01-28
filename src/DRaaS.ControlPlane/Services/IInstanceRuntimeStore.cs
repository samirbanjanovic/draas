using DRaaS.ControlPlane.Models;

namespace DRaaS.ControlPlane.Services;

public interface IInstanceRuntimeStore
{
    /// <summary>
    /// Saves or updates runtime information for an instance
    /// </summary>
    Task SaveAsync(InstanceRuntimeInfo runtimeInfo);

    /// <summary>
    /// Gets runtime information for a specific instance
    /// </summary>
    Task<InstanceRuntimeInfo?> GetAsync(string instanceId);

    /// <summary>
    /// Gets all runtime information entries
    /// </summary>
    Task<IEnumerable<InstanceRuntimeInfo>> GetAllAsync();

    /// <summary>
    /// Gets all runtime information for a specific platform type
    /// </summary>
    Task<IEnumerable<InstanceRuntimeInfo>> GetByPlatformAsync(PlatformType platformType);

    /// <summary>
    /// Deletes runtime information for an instance
    /// </summary>
    Task<bool> DeleteAsync(string instanceId);

    /// <summary>
    /// Checks if runtime information exists for an instance
    /// </summary>
    Task<bool> ExistsAsync(string instanceId);
}
