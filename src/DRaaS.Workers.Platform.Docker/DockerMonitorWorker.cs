using DRaaS.Core.Messaging;
using DRaaS.Core.Messaging.Events;
using DRaaS.Core.Models;
using DRaaS.Core.Services.Storage;

namespace DRaaS.Workers.Platform.Docker;

/// <summary>
/// Background service that monitors Docker platform instances
/// and publishes status change events.
/// </summary>
public class DockerMonitorWorker : BackgroundService
{
    private readonly IMessageBus _messageBus;
    private readonly IInstanceRuntimeStore _runtimeStore;
    private readonly ILogger<DockerMonitorWorker> _logger;
    private readonly TimeSpan _monitorInterval = TimeSpan.FromSeconds(15);

    public DockerMonitorWorker(
        IMessageBus messageBus,
        IInstanceRuntimeStore runtimeStore,
        ILogger<DockerMonitorWorker> logger)
    {
        _messageBus = messageBus;
        _runtimeStore = runtimeStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Docker Monitor Worker starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorInstancesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring Docker instances");
            }

            await Task.Delay(_monitorInterval, stoppingToken);
        }

        _logger.LogInformation("Docker Monitor Worker stopped");
    }

    private async Task MonitorInstancesAsync(CancellationToken cancellationToken)
    {
        var instances = await _runtimeStore.GetByPlatformAsync(PlatformType.Docker);

        foreach (var instance in instances)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                // TODO: Implement actual Docker container health check
                // 1. Execute: docker ps -a --filter id=<container-id> --format "{{.Status}}"
                // 2. Parse status (Up, Exited, Dead, etc.)
                // For now, we'll just log the monitoring check

                _logger.LogDebug("Docker instance {InstanceId} monitoring check (Container: {ContainerId})",
                    instance.InstanceId, instance.ContainerId);

                // Example of how to detect and publish status changes:
                // if (actualStatus != instance.Status)
                // {
                //     var updatedInfo = instance with
                //     {
                //         Status = actualStatus,
                //         StoppedAt = DateTime.UtcNow
                //     };
                //     await _runtimeStore.SaveAsync(updatedInfo);
                //
                //     await PublishStatusChangeAsync(new InstanceStatusChangedEvent
                //     {
                //         InstanceId = instance.InstanceId,
                //         OldStatus = instance.Status,
                //         NewStatus = actualStatus,
                //         Source = "DockerMonitorWorker"
                //     });
                // }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring Docker instance {InstanceId}", instance.InstanceId);
            }
        }
    }

    private async Task PublishStatusChangeAsync(InstanceStatusChangedEvent statusEvent)
    {
        try
        {
            await _messageBus.PublishAsync(Channels.StatusEvents, statusEvent);
            _logger.LogInformation("Published status change for Docker instance {InstanceId}: {OldStatus} -> {NewStatus}",
                statusEvent.InstanceId, statusEvent.OldStatus, statusEvent.NewStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish status change event for Docker instance {InstanceId}", statusEvent.InstanceId);
        }
    }
}
