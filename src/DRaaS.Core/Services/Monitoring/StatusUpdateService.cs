using DRaaS.Core.Models;
using DRaaS.Core.Services.Storage;
using DRaaS.Core.Messaging;
using DRaaS.Core.Messaging.Events;
using System.Collections.Concurrent;

namespace DRaaS.Core.Services.Monitoring;

/// <summary>
/// Centralized status update service that acts as a message bus for instance status changes.
/// Receives updates from both local monitors (polling) and external daemons (push).
/// Publishes status changes to Redis message bus for distributed communication.
/// Maintains a rolling buffer of recent status changes for API polling.
/// </summary>
public class StatusUpdateService : IStatusUpdateService
{
    private readonly IInstanceRuntimeStore _runtimeStore;
    private readonly IMessageBus _messageBus;
    private readonly ConcurrentQueue<StatusChangeRecord> _recentChanges = new();
    private const int MaxRecentChanges = 1000; // Keep last 1000 changes

    public StatusUpdateService(
        IInstanceRuntimeStore runtimeStore,
        IMessageBus messageBus)
    {
        _runtimeStore = runtimeStore;
        _messageBus = messageBus;
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
            var timestamp = DateTime.UtcNow;

            // Update runtime store with new status
            var updatedInfo = currentInfo with
            {
                Status = newStatus,
                StoppedAt = newStatus == InstanceStatus.Stopped ? timestamp : currentInfo.StoppedAt
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

            // Add to recent changes buffer for API polling
            var changeRecord = new StatusChangeRecord
            {
                InstanceId = instanceId,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                Source = source,
                Timestamp = timestamp,
                Metadata = metadata ?? new Dictionary<string, string>()
            };

            _recentChanges.Enqueue(changeRecord);

            // Trim buffer if too large
            while (_recentChanges.Count > MaxRecentChanges)
            {
                _recentChanges.TryDequeue(out _);
            }

            // Publish to Redis message bus for distributed communication
            try
            {
                await _messageBus.PublishAsync(Channels.StatusEvents, new InstanceStatusChangedEvent
                {
                    InstanceId = instanceId,
                    OldStatus = oldStatus,
                    NewStatus = newStatus,
                    Source = source
                });
            }
            catch (Exception)
            {
                // Log error but don't fail the status update
                // In-memory event will still be raised below
            }

            // Raise in-memory event to notify local subscribers
            StatusChanged?.Invoke(this, new StatusUpdateEventArgs
            {
                InstanceId = instanceId,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                Source = source,
                Timestamp = timestamp,
                Metadata = metadata ?? new Dictionary<string, string>()
            });
        }
    }

    public async Task<InstanceRuntimeInfo?> GetLastKnownStatusAsync(string instanceId)
    {
        return await _runtimeStore.GetAsync(instanceId);
    }

    public Task<IEnumerable<StatusChangeRecord>> GetRecentChangesAsync(
        DateTime since,
        InstanceStatus? statusFilter = null)
    {
        var filtered = _recentChanges
            .Where(change => change.Timestamp >= since)
            .Where(change => statusFilter == null || change.NewStatus == statusFilter)
            .OrderBy(change => change.Timestamp)
            .AsEnumerable();

        return Task.FromResult(filtered);
    }
}
