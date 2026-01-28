using DRaaS.Core.Models;
using DRaaS.Core.Services.Monitoring;
using Microsoft.AspNetCore.JsonPatch;

namespace DRaaS.Reconciliation;

/// <summary>
/// HTTP client interface for calling ControlPlane APIs.
/// Provides methods to manage instances and configurations through the ControlPlane REST API.
/// </summary>
public interface IReconciliationApiClient
{
    /// <summary>
    /// Gets instance details from ControlPlane.
    /// </summary>
    /// <param name="instanceId">The instance identifier</param>
    /// <returns>The instance details, or null if not found</returns>
    Task<DrasiInstance?> GetInstanceAsync(string instanceId);

    /// <summary>
    /// Gets all instances from ControlPlane.
    /// </summary>
    /// <returns>Collection of all instances</returns>
    Task<IEnumerable<DrasiInstance>> GetAllInstancesAsync();

    /// <summary>
    /// Starts an instance with the specified configuration.
    /// </summary>
    /// <param name="instanceId">The instance identifier</param>
    /// <param name="configuration">Optional configuration to apply during start</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> StartInstanceAsync(string instanceId, Configuration? configuration = null);

    /// <summary>
    /// Stops a running instance.
    /// </summary>
    /// <param name="instanceId">The instance identifier</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> StopInstanceAsync(string instanceId);

    /// <summary>
    /// Restarts an instance.
    /// </summary>
    /// <param name="instanceId">The instance identifier</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> RestartInstanceAsync(string instanceId);

    /// <summary>
    /// Gets the configuration for a specific instance.
    /// </summary>
    /// <param name="instanceId">The instance identifier</param>
    /// <returns>The configuration, or null if not found</returns>
    Task<Configuration?> GetConfigurationAsync(string instanceId);

    /// <summary>
    /// Updates the configuration for a specific instance using a JSON Patch document.
    /// </summary>
    /// <param name="instanceId">The instance identifier</param>
    /// <param name="patch">JSON Patch document with configuration changes</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> UpdateConfigurationAsync(string instanceId, JsonPatchDocument<Configuration> patch);

    /// <summary>
    /// Gets recent status changes from ControlPlane for event-driven reconciliation.
    /// </summary>
    /// <param name="since">Get changes since this timestamp</param>
    /// <param name="statusFilter">Optional filter (e.g., ConfigurationChanged)</param>
    /// <returns>List of status change records</returns>
    Task<IEnumerable<StatusChangeRecord>> GetRecentStatusChangesAsync(DateTime since, InstanceStatus? statusFilter = null);
}
