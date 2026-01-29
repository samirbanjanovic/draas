using DRaaS.Core.Messaging;
using DRaaS.Core.Messaging.Events;
using DRaaS.Core.Models;
using DRaaS.Core.Services.Storage;

namespace DRaaS.Workers.Platform.Process;

/// <summary>
/// Background service that monitors Process platform instances
/// and publishes status change events.
/// </summary>
public class ProcessMonitorWorker : BackgroundService
{
    private readonly IMessageBus _messageBus;
    private readonly ProcessInstanceManager _instanceManager;
    private readonly IInstanceRuntimeStore _runtimeStore;
    private readonly ILogger<ProcessMonitorWorker> _logger;
    private readonly TimeSpan _monitorInterval = TimeSpan.FromSeconds(10);

    public ProcessMonitorWorker(
        IMessageBus messageBus,
        ProcessInstanceManager instanceManager,
        IInstanceRuntimeStore runtimeStore,
        ILogger<ProcessMonitorWorker> logger)
    {
        _messageBus = messageBus;
        _instanceManager = instanceManager;
        _runtimeStore = runtimeStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Process Monitor Worker starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorInstancesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring instances");
            }

            await Task.Delay(_monitorInterval, stoppingToken);
        }

        _logger.LogInformation("Process Monitor Worker stopped");
    }

    private async Task MonitorInstancesAsync(CancellationToken cancellationToken)
    {
        var trackedProcesses = _instanceManager.TrackedProcesses;

        foreach (var (instanceId, process) in trackedProcesses)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                // Check if process has exited
                if (process.HasExited)
                {
                    _logger.LogWarning("Instance {InstanceId} process (PID: {ProcessId}) has exited unexpectedly with code {ExitCode}",
                        instanceId, process.Id, process.ExitCode);

                    // Update runtime store
                    var runtimeInfo = await _runtimeStore.GetAsync(instanceId);
                    if (runtimeInfo != null)
                    {
                        var updatedInfo = runtimeInfo with
                        {
                            Status = InstanceStatus.Error,
                            StoppedAt = DateTime.UtcNow
                        };
                        await _runtimeStore.SaveAsync(updatedInfo);

                        // Publish status change event
                        await PublishStatusChangeAsync(new InstanceStatusChangedEvent
                        {
                            InstanceId = instanceId,
                            OldStatus = InstanceStatus.Running,
                            NewStatus = InstanceStatus.Error,
                            Source = "ProcessMonitorWorker"
                        });
                    }

                    // Remove from tracked processes
                    trackedProcesses.TryRemove(instanceId, out _);
                }
                else
                {
                    // Process is running - could check additional health metrics here
                    _logger.LogDebug("Instance {InstanceId} is healthy (PID: {ProcessId})",
                        instanceId, process.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring instance {InstanceId}", instanceId);
            }
        }
    }

    private async Task PublishStatusChangeAsync(InstanceStatusChangedEvent statusEvent)
    {
        try
        {
            await _messageBus.PublishAsync(Channels.StatusEvents, statusEvent);
            _logger.LogInformation("Published status change for instance {InstanceId}: {OldStatus} -> {NewStatus}",
                statusEvent.InstanceId, statusEvent.OldStatus, statusEvent.NewStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish status change event for instance {InstanceId}", statusEvent.InstanceId);
        }
    }
}
