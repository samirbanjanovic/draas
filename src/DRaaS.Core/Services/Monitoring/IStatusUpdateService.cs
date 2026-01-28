using DRaaS.Core.Models;

namespace DRaaS.Core.Services.Monitoring;

/// <summary>
/// Centralized service for receiving and publishing instance status updates.
/// Supports both polling-based (Process) and push-based (Docker/AKS daemon) status updates.
/// </summary>
public interface IStatusUpdateService
{
    /// <summary>
    /// Event raised when an instance status changes.
    /// Subscribers can react to status changes (logging, notifications, UI updates, etc.)
    /// </summary>
    event EventHandler<StatusUpdateEventArgs>? StatusChanged;

    /// <summary>
    /// Publishes a status update to all subscribers.
    /// Called by local monitors (polling) or external daemons (push).
    /// </summary>
    /// <param name="instanceId">The instance identifier</param>
    /// <param name="newStatus">The new status</param>
    /// <param name="source">The source of the update (e.g., "ProcessMonitor", "DockerDaemon", "AKSDaemon")</param>
    /// <param name="metadata">Optional metadata about the status change</param>
    Task PublishStatusUpdateAsync(string instanceId, InstanceStatus newStatus, string source, Dictionary<string, string>? metadata = null);

    /// <summary>
    /// Gets the last known status for an instance.
    /// </summary>
    /// <param name="instanceId">The instance identifier</param>
    /// <returns>The runtime information, or null if not found</returns>
    Task<InstanceRuntimeInfo?> GetLastKnownStatusAsync(string instanceId);
}

/// <summary>
/// Event arguments for status change events.
/// </summary>
public class StatusUpdateEventArgs : EventArgs
{
    public string InstanceId { get; init; } = string.Empty;
    public InstanceStatus OldStatus { get; init; }
    public InstanceStatus NewStatus { get; init; }
    public string Source { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}
