using DRaaS.Core.Models;
using DRaaS.Core.Services.Reconciliation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DRaaS.Reconciliation;

/// <summary>
/// Background service that runs periodic reconciliation loops and polls for configuration change events.
/// All operations are performed through ControlPlane API (no direct Core dependencies).
/// </summary>
public class ReconciliationBackgroundService : BackgroundService
{
    private readonly IReconciliationService _reconciliationService;
    private readonly IReconciliationApiClient _apiClient;
    private readonly ReconciliationOptions _options;
    private readonly ILogger<ReconciliationBackgroundService> _logger;
    private DateTime _lastEventPoll = DateTime.UtcNow;

    public ReconciliationBackgroundService(
        IReconciliationService reconciliationService,
        IReconciliationApiClient apiClient,
        IOptions<ReconciliationOptions> options,
        ILogger<ReconciliationBackgroundService> logger)
    {
        _reconciliationService = reconciliationService;
        _apiClient = apiClient;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Reconciliation Background Service starting...");

        if (_options.EnableEventDrivenReconciliation)
        {
            _logger.LogInformation("Event-driven reconciliation enabled (API polling mode)");
        }

        // Run periodic reconciliation if enabled
        if (_options.EnableAutoReconciliation && _options.PollingInterval > TimeSpan.Zero)
        {
            _logger.LogInformation(
                "Periodic reconciliation enabled with interval: {Interval}",
                _options.PollingInterval);

            await RunPeriodicReconciliationAsync(stoppingToken);
        }
        else
        {
            _logger.LogInformation("Periodic reconciliation disabled");

            // Just wait for cancellation if only event-driven is enabled
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }

    private async Task RunPeriodicReconciliationAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(_options.PollingInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                _logger.LogDebug("Starting periodic reconciliation cycle");

                try
                {
                    // Poll for configuration change events if enabled
                    if (_options.EnableEventDrivenReconciliation)
                    {
                        await PollForStatusChangesAsync(stoppingToken);
                    }

                    // Run full reconciliation cycle
                    var results = await _reconciliationService.ReconcileAllInstancesAsync(stoppingToken);

                    var driftCount = results.Count(r => r.HasDrift);
                    var reconciledCount = results.Count(r => r.ReconciliationAttempted && r.ReconciliationSuccessful == true);
                    var failedCount = results.Count(r => r.ReconciliationAttempted && r.ReconciliationSuccessful == false);

                    _logger.LogInformation(
                        "Reconciliation cycle complete. Checked: {Total}, Drift detected: {Drift}, Reconciled: {Success}, Failed: {Failed}",
                        results.Count(),
                        driftCount,
                        reconciledCount,
                        failedCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during periodic reconciliation cycle");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Periodic reconciliation cancelled");
        }
    }

    private async Task PollForStatusChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Get status changes since last poll (ConfigurationChanged only)
            var changes = await _apiClient.GetRecentStatusChangesAsync(
                _lastEventPoll,
                InstanceStatus.ConfigurationChanged);

            foreach (var change in changes)
            {
                _logger.LogInformation(
                    "Configuration change detected for instance {InstanceId}, triggering reconciliation",
                    change.InstanceId);

                try
                {
                    var result = await _reconciliationService.ReconcileInstanceAsync(change.InstanceId, cancellationToken);

                    if (result.ReconciliationAttempted && result.ReconciliationSuccessful == true)
                    {
                        _logger.LogInformation(
                            "Successfully reconciled instance {InstanceId} using {Strategy}",
                            change.InstanceId,
                            result.StrategyUsed);
                    }
                    else if (result.ReconciliationAttempted && result.ReconciliationSuccessful == false)
                    {
                        _logger.LogWarning(
                            "Failed to reconcile instance {InstanceId}: {Error}",
                            change.InstanceId,
                            result.ErrorMessage);
                    }
                    else
                    {
                        _logger.LogDebug(
                            "No drift detected for instance {InstanceId}",
                            change.InstanceId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error during event-driven reconciliation for instance {InstanceId}",
                        change.InstanceId);
                }
            }

            // Update last poll timestamp
            _lastEventPoll = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling for status changes from ControlPlane");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reconciliation Background Service stopping...");
        return base.StopAsync(cancellationToken);
    }
}
