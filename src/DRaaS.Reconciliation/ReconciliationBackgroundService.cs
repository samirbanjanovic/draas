using DRaaS.Core.Services.Monitoring;
using DRaaS.Core.Services.Reconciliation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DRaaS.Reconciliation;

/// <summary>
/// Background service that runs periodic reconciliation loops and responds to configuration change events.
/// </summary>
public class ReconciliationBackgroundService : BackgroundService
{
    private readonly IReconciliationService _reconciliationService;
    private readonly IStatusUpdateService _statusUpdateService;
    private readonly ReconciliationOptions _options;
    private readonly ILogger<ReconciliationBackgroundService> _logger;

    public ReconciliationBackgroundService(
        IReconciliationService reconciliationService,
        IStatusUpdateService statusUpdateService,
        IOptions<ReconciliationOptions> options,
        ILogger<ReconciliationBackgroundService> logger)
    {
        _reconciliationService = reconciliationService;
        _statusUpdateService = statusUpdateService;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Reconciliation Background Service starting...");

        // Subscribe to configuration change events if enabled
        if (_options.EnableEventDrivenReconciliation)
        {
            _statusUpdateService.StatusChanged += OnStatusChanged;
            _logger.LogInformation("Event-driven reconciliation enabled");
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

    private async void OnStatusChanged(object? sender, StatusUpdateEventArgs e)
    {
        // Only react to configuration changes
        if (e.NewStatus != Core.Models.InstanceStatus.ConfigurationChanged)
        {
            return;
        }

        _logger.LogInformation(
            "Configuration change detected for instance {InstanceId}, triggering reconciliation",
            e.InstanceId);

        try
        {
            var result = await _reconciliationService.ReconcileInstanceAsync(e.InstanceId);

            if (result.ReconciliationAttempted && result.ReconciliationSuccessful == true)
            {
                _logger.LogInformation(
                    "Successfully reconciled instance {InstanceId} using {Strategy}",
                    e.InstanceId,
                    result.StrategyUsed);
            }
            else if (result.ReconciliationAttempted && result.ReconciliationSuccessful == false)
            {
                _logger.LogWarning(
                    "Failed to reconcile instance {InstanceId}: {Error}",
                    e.InstanceId,
                    result.ErrorMessage);
            }
            else
            {
                _logger.LogDebug(
                    "No drift detected for instance {InstanceId}",
                    e.InstanceId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error during event-driven reconciliation for instance {InstanceId}",
                e.InstanceId);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reconciliation Background Service stopping...");

        // Unsubscribe from events
        if (_options.EnableEventDrivenReconciliation)
        {
            _statusUpdateService.StatusChanged -= OnStatusChanged;
        }

        return base.StopAsync(cancellationToken);
    }
}
