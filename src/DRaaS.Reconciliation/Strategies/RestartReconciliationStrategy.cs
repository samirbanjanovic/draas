using DRaaS.Core.Models;
using DRaaS.Core.Services.Reconciliation;

namespace DRaaS.Reconciliation.Strategies;

/// <summary>
/// Simple restart strategy: Stop the instance, then start it with the new configuration.
/// This is the most straightforward approach but causes downtime.
/// All operations are performed through the ControlPlane API to ensure centralized management.
/// </summary>
public class RestartReconciliationStrategy : IReconciliationStrategy
{
    private readonly IReconciliationApiClient _apiClient;
    private readonly ILogger<RestartReconciliationStrategy> _logger;

    public RestartReconciliationStrategy(
        IReconciliationApiClient apiClient,
        ILogger<RestartReconciliationStrategy> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public ReconciliationStrategy StrategyType => ReconciliationStrategy.Restart;

    public async Task<bool> ApplyAsync(
        string instanceId,
        Configuration desiredConfiguration,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Starting restart reconciliation for instance {InstanceId}",
                instanceId);

            // 1. Verify instance exists
            var instance = await _apiClient.GetInstanceAsync(instanceId);
            if (instance == null)
            {
                _logger.LogError("Instance {InstanceId} not found", instanceId);
                return false;
            }

            // 2. Stop the instance via ControlPlane API
            _logger.LogDebug("Stopping instance {InstanceId}", instanceId);
            var stopped = await _apiClient.StopInstanceAsync(instanceId);
            if (!stopped)
            {
                _logger.LogError("Failed to stop instance {InstanceId}", instanceId);
                return false;
            }

            // 3. Small delay to ensure clean shutdown
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            // 4. Start the instance with new configuration via ControlPlane API
            _logger.LogDebug(
                "Starting instance {InstanceId} with new configuration",
                instanceId);
            var started = await _apiClient.StartInstanceAsync(instanceId, desiredConfiguration);
            if (!started)
            {
                _logger.LogError("Failed to start instance {InstanceId}", instanceId);
                return false;
            }

            _logger.LogInformation(
                "Successfully reconciled instance {InstanceId} using restart strategy",
                instanceId);
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Reconciliation cancelled for instance {InstanceId}",
                instanceId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error during restart reconciliation for instance {InstanceId}",
                instanceId);
            return false;
        }
    }
}
