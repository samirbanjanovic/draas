using DRaaS.Core.Messaging;
using DRaaS.Core.Messaging.Commands;
using DRaaS.Core.Messaging.Events;
using DRaaS.Core.Messaging.Responses;
using DRaaS.Core.Models;
using DRaaS.Core.Providers;

namespace DRaaS.Workers.Platform.Docker;

/// <summary>
/// Background service that subscribes to Docker platform instance commands
/// and executes them using DockerInstanceManager.
/// </summary>
public class DockerCommandWorker : BackgroundService
{
    private readonly IMessageBus _messageBus;
    private readonly IDrasiServerInstanceManager _instanceManager;
    private readonly ILogger<DockerCommandWorker> _logger;

    public DockerCommandWorker(
        IMessageBus messageBus,
        IDrasiServerInstanceManager instanceManager,
        ILogger<DockerCommandWorker> logger)
    {
        _messageBus = messageBus;
        _instanceManager = instanceManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Docker Command Worker starting...");

        var commandChannel = Channels.GetInstanceCommandChannel(PlatformType.Docker);
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

        _logger.LogInformation("Docker Command Worker subscribed and running");

        // Keep the worker alive
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleStartInstanceAsync(StartInstanceCommand command, CancellationToken cancellationToken)
    {
        StartInstanceResponse response;
        
        try
        {
            _logger.LogInformation("Starting Docker instance {InstanceId}", command.InstanceId);

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
                    Source = "DockerCommandWorker",
                    CorrelationId = command.CorrelationId
                });
            }
            else
            {
                var runtimeInfo = await _instanceManager.StartInstanceAsync(command.InstanceId, command.Configuration);

                _logger.LogInformation("Docker instance {InstanceId} started successfully with Container ID {ContainerId}", 
                    command.InstanceId, runtimeInfo.ContainerId);

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
                    Source = "DockerCommandWorker",
                    CorrelationId = command.CorrelationId
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Docker instance {InstanceId}", command.InstanceId);

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
                Source = "DockerCommandWorker",
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
            _logger.LogInformation("Stopping Docker instance {InstanceId}", command.InstanceId);

            var runtimeInfo = await _instanceManager.StopInstanceAsync(command.InstanceId);

            _logger.LogInformation("Docker instance {InstanceId} stopped successfully", command.InstanceId);

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
                Source = "DockerCommandWorker",
                CorrelationId = command.CorrelationId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop Docker instance {InstanceId}", command.InstanceId);
            
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
            _logger.LogInformation("Restarting Docker instance {InstanceId}", command.InstanceId);

            // Stop first
            await _instanceManager.StopInstanceAsync(command.InstanceId);

            // Small delay for clean shutdown
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            // TODO: Restart requires fetching configuration from configuration provider
            _logger.LogWarning("Docker instance {InstanceId} stopped for restart - configuration retrieval needed", command.InstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart Docker instance {InstanceId}", command.InstanceId);
        }
    }

    private async Task HandleDeleteInstanceAsync(DeleteInstanceCommand command, CancellationToken cancellationToken)
    {
        DeleteInstanceResponse response;
        
        try
        {
            _logger.LogInformation("Deleting Docker instance {InstanceId}", command.InstanceId);

            // Stop the instance first
            try
            {
                await _instanceManager.StopInstanceAsync(command.InstanceId);
            }
            catch (KeyNotFoundException)
            {
                // Instance might not be running, continue with deletion
            }

            _logger.LogInformation("Docker instance {InstanceId} deleted successfully", command.InstanceId);

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
            _logger.LogError(ex, "Failed to delete Docker instance {InstanceId}", command.InstanceId);
            
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
