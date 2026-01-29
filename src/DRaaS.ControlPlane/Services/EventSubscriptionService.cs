using DRaaS.Core.Messaging;
using DRaaS.Core.Messaging.Events;
using DRaaS.Core.Services.Instance;

namespace DRaaS.ControlPlane.Services;

/// <summary>
/// Background service that subscribes to events from platform workers
/// and updates ControlPlane state accordingly.
/// </summary>
public class EventSubscriptionService : BackgroundService
{
    private readonly IMessageBus _messageBus;
    private readonly IDrasiInstanceService _instanceService;
    private readonly ILogger<EventSubscriptionService> _logger;

    public EventSubscriptionService(
        IMessageBus messageBus,
        IDrasiInstanceService instanceService,
        ILogger<EventSubscriptionService> logger)
    {
        _messageBus = messageBus;
        _instanceService = instanceService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Event Subscription Service starting...");

        // Subscribe to status change events from all workers
        await _messageBus.SubscribeAsync<InstanceStatusChangedEvent>(
            Channels.StatusEvents,
            async (statusEvent) =>
            {
                try
                {
                    _logger.LogInformation(
                        "Received status change event for instance {InstanceId}: {OldStatus} -> {NewStatus} (Source: {Source})",
                        statusEvent.InstanceId,
                        statusEvent.OldStatus,
                        statusEvent.NewStatus,
                        statusEvent.Source);

                    // Update instance status in ControlPlane metadata
                    await _instanceService.UpdateInstanceStatusAsync(
                        statusEvent.InstanceId,
                        statusEvent.NewStatus);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error processing status change event for instance {InstanceId}",
                        statusEvent.InstanceId);
                }
            },
            stoppingToken);

        // Subscribe to instance lifecycle events
        await _messageBus.SubscribeAsync<InstanceStartedEvent>(
            Channels.InstanceEvents,
            async (startedEvent) =>
            {
                try
                {
                    _logger.LogInformation(
                        "Instance {InstanceId} started",
                        startedEvent.InstanceId);

                    // ControlPlane already updated status via StartInstance response
                    // This is for audit/logging purposes
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error processing instance started event for {InstanceId}",
                        startedEvent.InstanceId);
                }
            },
            stoppingToken);

        await _messageBus.SubscribeAsync<InstanceStoppedEvent>(
            Channels.InstanceEvents,
            async (stoppedEvent) =>
            {
                try
                {
                    _logger.LogInformation(
                        "Instance {InstanceId} stopped",
                        stoppedEvent.InstanceId);

                    // ControlPlane already updated status via StopInstance response
                    // This is for audit/logging purposes
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error processing instance stopped event for {InstanceId}",
                        stoppedEvent.InstanceId);
                }
            },
            stoppingToken);

        await _messageBus.SubscribeAsync<InstanceDeletedEvent>(
            Channels.InstanceEvents,
            async (deletedEvent) =>
            {
                try
                {
                    _logger.LogInformation(
                        "Instance {InstanceId} deleted",
                        deletedEvent.InstanceId);

                    // ControlPlane already deleted metadata via DeleteInstance response
                    // This is for audit/logging purposes
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error processing instance deleted event for {InstanceId}",
                        deletedEvent.InstanceId);
                }
            },
            stoppingToken);

        // Keep the service running
        await Task.Delay(Timeout.Infinite, stoppingToken);

        _logger.LogInformation("Event Subscription Service stopped");
    }
}
