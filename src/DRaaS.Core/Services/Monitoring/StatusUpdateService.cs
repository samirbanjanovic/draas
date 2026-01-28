using DRaaS.Core.Models;
using DRaaS.Core.Services.Storage;

namespace DRaaS.Core.Services.Monitoring;

/// <summary>
/// Centralized status update service that acts as a message bus for instance status changes.
/// Receives updates from both local monitors (polling) and external daemons (push).
/// </summary>
public class StatusUpdateService : IStatusUpdateService
{
    private readonly IInstanceRuntimeStore _runtimeStore;

    public StatusUpdateService(IInstanceRuntimeStore runtimeStore)
    {
        _runtimeStore = runtimeStore;
    }

    public event EventHandler<StatusUpdateEventArgs>? StatusChanged;

    public async Task PublishStatusUpdateAsync(
        string instanceId, 
        InstanceStatus newStatus, 
        string source, 
        Dictionary<string, string>? metadata = null)
    {
        // Get current status from runtime store
        var currentInfo = await _runtimeStore.GetAsync(instanceId);
        if (currentInfo == null)
        {
            // Instance not found - could be a race condition or invalid instanceId
            return;
        }

        var oldStatus = currentInfo.Status;

        // Only update if status actually changed
        if (oldStatus != newStatus)
        {
            // Update runtime store with new status
            var updatedInfo = currentInfo with
            {
                Status = newStatus,
                StoppedAt = newStatus == InstanceStatus.Stopped ? DateTime.UtcNow : currentInfo.StoppedAt
            };

            // Merge metadata if provided
            if (metadata != null && metadata.Count > 0)
            {
                foreach (var kvp in metadata)
                {
                    updatedInfo.RuntimeMetadata[kvp.Key] = kvp.Value;
                }
            }

            await _runtimeStore.SaveAsync(updatedInfo);

            // Raise event to notify subscribers
            StatusChanged?.Invoke(this, new StatusUpdateEventArgs
            {
                InstanceId = instanceId,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                Source = source,
                Timestamp = DateTime.UtcNow,
                Metadata = metadata ?? new Dictionary<string, string>()
            });
        }
    }

    public async Task<InstanceRuntimeInfo?> GetLastKnownStatusAsync(string instanceId)
    {
        return await _runtimeStore.GetAsync(instanceId);
    }
}
