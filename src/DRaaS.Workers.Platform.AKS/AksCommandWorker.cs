using DRaaS.Core.Messaging;
using DRaaS.Core.Messaging.Commands;
using DRaaS.Core.Messaging.Events;
using DRaaS.Core.Messaging.Responses;
using DRaaS.Core.Models;
using DRaaS.Core.Providers;

namespace DRaaS.Workers.Platform.AKS;

/// <summary>
/// Background service that subscribes to AKS platform instance commands
/// and executes them using AKSInstanceManager.
/// </summary>
public class AksCommandWorker : BackgroundService
{
    private readonly IMessageBus _messageBus;
    private readonly IDrasiServerInstanceManager _instanceManager;
    private readonly ILogger<AksCommandWorker> _logger;

    public AksCommandWorker(
        IMessageBus messageBus,
        IDrasiServerInstanceManager instanceManager,
        ILogger<AksCommandWorker> logger)
    {
        _messageBus = messageBus;
        _instanceManager = instanceManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AKS Command Worker starting...");

        var commandChannel = Channels.GetInstanceCommandChannel(PlatformType.AKS);
        _logger.LogInformation("Subscribing to channel: {Channel}", commandChannel);

        await _messageBus.SubscribeAsync<StartInstanceCommand>(commandChannel, async (command) =>
        {
            await HandleStartInstanceAsync(command, stoppingToken);
        });

        await _messageBus.SubscribeAsync<StopInstanceCommand>(commandChannel, async (command) =>
        {
            await HandleStopInstanceAsync(command, stoppingToken);
        });

        await _messageBus.SubscribeAsync<RestartInstanceCommand>(commandChannel, async (command) =>
        {
            await HandleRestartInstanceAsync(command, stoppingToken);
        });

        await _messageBus.SubscribeAsync<DeleteInstanceCommand>(commandChannel, async (command) =>
        {
            await HandleDeleteInstanceAsync(command, stoppingToken);
        });

        _logger.LogInformation("AKS Command Worker subscribed and running");

        // Keep the worker alive
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleStartInstanceAsync(StartInstanceCommand command, CancellationToken cancellationToken)
    {
        StartInstanceResponse response;
        
        try
        {
            _logger.LogInformation("Starting AKS instance {InstanceId}", command.InstanceId);

            if (command.Configuration == null)
            {
                _logger.LogError("Start command for instance {InstanceId} has no configuration", command.InstanceId);
                
                response = new StartInstanceResponse
                {
                    InstanceId = command.InstanceId,
                    Success = false,
                    ErrorMessage = "Configuration is required",
                    CorrelationId = command.CorrelationId
                };

                await PublishInstanceEventAsync(new InstanceStatusChangedEvent
                {
                    InstanceId = command.InstanceId,
                    OldStatus = InstanceStatus.Stopped,
                    NewStatus = InstanceStatus.Error,
                    Source = "AksCommandWorker",
                    CorrelationId = command.CorrelationId
                });
            }
            else
            {
                var runtimeInfo = await _instanceManager.StartInstanceAsync(command.InstanceId, command.Configuration);

                _logger.LogInformation("AKS instance {InstanceId} started successfully with Pod Name {PodName}", 
                    command.InstanceId, runtimeInfo.PodName);

                response = new StartInstanceResponse
                {
                    InstanceId = command.InstanceId,
                    Success = true,
                    RuntimeInfo = runtimeInfo,
                    CorrelationId = command.CorrelationId
                };

                await PublishInstanceEventAsync(new InstanceStartedEvent
                {
                    InstanceId = command.InstanceId,
                    CorrelationId = command.CorrelationId
                });

                await PublishInstanceEventAsync(new InstanceStatusChangedEvent
                {
                    InstanceId = command.InstanceId,
                    OldStatus = InstanceStatus.Stopped,
                    NewStatus = InstanceStatus.Running,
                    Source = "AksCommandWorker",
                    CorrelationId = command.CorrelationId
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start AKS instance {InstanceId}", command.InstanceId);

            response = new StartInstanceResponse
            {
                InstanceId = command.InstanceId,
                Success = false,
                ErrorMessage = ex.Message,
                CorrelationId = command.CorrelationId
            };

            await PublishInstanceEventAsync(new InstanceStatusChangedEvent
            {
                InstanceId = command.InstanceId,
                OldStatus = InstanceStatus.Stopped,
                NewStatus = InstanceStatus.Error,
                Source = "AksCommandWorker",
                CorrelationId = command.CorrelationId
            });
        }

        // Send response back if reply channel is specified
        if (!string.IsNullOrEmpty(command.ReplyChannel))
        {
            await _messageBus.PublishAsync(command.ReplyChannel, response);
        }
    }

    private async Task HandleStopInstanceAsync(StopInstanceCommand command, CancellationToken cancellationToken)
    {
        StopInstanceResponse response;
        
        try
        {
            _logger.LogInformation("Stopping AKS instance {InstanceId}", command.InstanceId);

            var runtimeInfo = await _instanceManager.StopInstanceAsync(command.InstanceId);

            _logger.LogInformation("AKS instance {InstanceId} stopped successfully", command.InstanceId);

            response = new StopInstanceResponse
            {
                InstanceId = command.InstanceId,
                Success = true,
                RuntimeInfo = runtimeInfo,
                CorrelationId = command.CorrelationId
            };

            await PublishInstanceEventAsync(new InstanceStoppedEvent
            {
                InstanceId = command.InstanceId,
                CorrelationId = command.CorrelationId
            });

            await PublishInstanceEventAsync(new InstanceStatusChangedEvent
            {
                InstanceId = command.InstanceId,
                OldStatus = InstanceStatus.Running,
                NewStatus = InstanceStatus.Stopped,
                Source = "AksCommandWorker",
                CorrelationId = command.CorrelationId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop AKS instance {InstanceId}", command.InstanceId);
            
            response = new StopInstanceResponse
            {
                InstanceId = command.InstanceId,
                Success = false,
                ErrorMessage = ex.Message,
                CorrelationId = command.CorrelationId
            };
        }

        // Send response back if reply channel is specified
        if (!string.IsNullOrEmpty(command.ReplyChannel))
        {
            await _messageBus.PublishAsync(command.ReplyChannel, response);
        }
    }

    private async Task HandleRestartInstanceAsync(RestartInstanceCommand command, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Restarting AKS instance {InstanceId}", command.InstanceId);

            // Stop first
            await _instanceManager.StopInstanceAsync(command.InstanceId);

            // Small delay for clean shutdown
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            // TODO: Restart requires fetching configuration from configuration provider
            _logger.LogWarning("AKS instance {InstanceId} stopped for restart - configuration retrieval needed", command.InstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart AKS instance {InstanceId}", command.InstanceId);
        }
    }

    private async Task HandleDeleteInstanceAsync(DeleteInstanceCommand command, CancellationToken cancellationToken)
    {
        DeleteInstanceResponse response;
        
        try
        {
            _logger.LogInformation("Deleting AKS instance {InstanceId}", command.InstanceId);

            // Stop the instance first
            try
            {
                await _instanceManager.StopInstanceAsync(command.InstanceId);
            }
            catch (KeyNotFoundException)
            {
                // Instance might not be running, continue with deletion
            }

            _logger.LogInformation("AKS instance {InstanceId} deleted successfully", command.InstanceId);

            response = new DeleteInstanceResponse
            {
                InstanceId = command.InstanceId,
                Success = true,
                CorrelationId = command.CorrelationId
            };

            await PublishInstanceEventAsync(new InstanceDeletedEvent
            {
                InstanceId = command.InstanceId,
                CorrelationId = command.CorrelationId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete AKS instance {InstanceId}", command.InstanceId);
            
            response = new DeleteInstanceResponse
            {
                InstanceId = command.InstanceId,
                Success = false,
                ErrorMessage = ex.Message,
                CorrelationId = command.CorrelationId
            };
        }

        // Send response back if reply channel is specified
        if (!string.IsNullOrEmpty(command.ReplyChannel))
        {
            await _messageBus.PublishAsync(command.ReplyChannel, response);
        }
    }

    private async Task PublishInstanceEventAsync(Event instanceEvent)
    {
        try
        {
            await _messageBus.PublishAsync(Channels.InstanceEvents, instanceEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType}", instanceEvent.GetType().Name);
        }
    }
}
