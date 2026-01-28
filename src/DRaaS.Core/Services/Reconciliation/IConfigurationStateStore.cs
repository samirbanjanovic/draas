using DRaaS.Core.Models;

namespace DRaaS.Core.Services.Reconciliation;

/// <summary>
/// Manages desired state and actual state for instance configurations.
/// Provides the source of truth for configuration reconciliation.
/// </summary>
public interface IConfigurationStateStore
{
    /// <summary>
    /// Gets the desired configuration state for an instance.
    /// This is the configuration that should be applied (source of truth).
    /// Typically comes from ConfigurationProvider, Git, Cosmos, SQL, etc.
    /// </summary>
    /// <param name="instanceId">The instance identifier</param>
    /// <returns>The desired configuration, or null if not found</returns>
    Task<Configuration?> GetDesiredStateAsync(string instanceId);

    /// <summary>
    /// Gets the actual configuration state currently running on the instance.
    /// This represents what the instance is actually using right now.
    /// </summary>
    /// <param name="instanceId">The instance identifier</param>
    /// <returns>The actual configuration, or null if not found</returns>
    Task<Configuration?> GetActualStateAsync(string instanceId);

    /// <summary>
    /// Sets the actual state after successful reconciliation.
    /// Called when configuration has been successfully applied to the running instance.
    /// </summary>
    /// <param name="instanceId">The instance identifier</param>
    /// <param name="configuration">The configuration that was applied</param>
    Task SetActualStateAsync(string instanceId, Configuration configuration);

    /// <summary>
    /// Gets all instance IDs that have desired state configurations.
    /// Used for reconciling all instances.
    /// </summary>
    /// <returns>Collection of instance identifiers</returns>
    Task<IEnumerable<string>> GetAllInstanceIdsAsync();

    /// <summary>
    /// Records a reconciliation action in the audit trail.
    /// </summary>
    /// <param name="instanceId">The instance identifier</param>
    /// <param name="action">Description of the reconciliation action taken</param>
    /// <param name="driftDetected">Whether drift was detected</param>
    /// <param name="timestamp">When the action occurred</param>
    Task RecordReconciliationActionAsync(
        string instanceId, 
        string action, 
        bool driftDetected, 
        DateTime timestamp);
}
