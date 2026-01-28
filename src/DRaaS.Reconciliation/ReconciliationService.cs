using DRaaS.Core.Models;
using DRaaS.Core.Services.Reconciliation;
using DRaaS.Reconciliation.Strategies;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace DRaaS.Reconciliation;

/// <summary>
/// Main reconciliation service that detects configuration drift and applies changes.
/// Supports multiple reconciliation strategies and maintains audit trail.
/// </summary>
public class ReconciliationService : IReconciliationService
{
    private readonly IConfigurationStateStore _stateStore;
    private readonly IEnumerable<IReconciliationStrategy> _strategies;
    private readonly ReconciliationOptions _options;

    public ReconciliationService(
        IConfigurationStateStore stateStore,
        IEnumerable<IReconciliationStrategy> strategies,
        IOptions<ReconciliationOptions> options)
    {
        _stateStore = stateStore;
        _strategies = strategies;
        _options = options.Value;
    }

    public async Task<DriftDetectionResult> ReconcileInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        return await ReconcileWithStrategyAsync(
            instanceId,
            _options.DefaultStrategy,
            cancellationToken);
    }

    public async Task<IEnumerable<DriftDetectionResult>> ReconcileAllInstancesAsync(
        CancellationToken cancellationToken = default)
    {
        var instanceIds = await _stateStore.GetAllInstanceIdsAsync();
        var results = new List<DriftDetectionResult>();

        // Use semaphore to limit concurrency
        using var semaphore = new SemaphoreSlim(_options.MaxConcurrentReconciliations);

        var tasks = instanceIds.Select(async instanceId =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await ReconcileInstanceAsync(instanceId, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }

    public async Task<DriftDetectionResult> DetectDriftAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        var desired = await _stateStore.GetDesiredStateAsync(instanceId);
        var actual = await _stateStore.GetActualStateAsync(instanceId);

        if (desired == null)
        {
            return new DriftDetectionResult
            {
                InstanceId = instanceId,
                HasDrift = false,
                ErrorMessage = "No desired state found for instance"
            };
        }

        // If no actual state exists, this is the first reconciliation
        if (actual == null)
        {
            return DriftDetectionResult.WithDrift(
                instanceId,
                desired,
                new Configuration(), // Empty actual state
                new List<string> { "Initial configuration - no actual state exists" });
        }

        // Compare configurations
        var differences = CompareConfigurations(desired, actual);

        if (differences.Count == 0)
        {
            return DriftDetectionResult.NoDrift(instanceId, desired);
        }

        return DriftDetectionResult.WithDrift(instanceId, desired, actual, differences);
    }

    public async Task<DriftDetectionResult> ReconcileWithStrategyAsync(
        string instanceId,
        ReconciliationStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        // 1. Detect drift
        var driftResult = await DetectDriftAsync(instanceId, cancellationToken);

        if (!driftResult.HasDrift)
        {
            // No drift, nothing to do
            if (_options.EnableAuditLogging)
            {
                await _stateStore.RecordReconciliationActionAsync(
                    instanceId,
                    "No drift detected",
                    false,
                    DateTime.UtcNow);
            }

            return driftResult with
            {
                ReconciliationAttempted = false
            };
        }

        // 2. Find the appropriate strategy
        var reconciliationStrategy = _strategies.FirstOrDefault(s => s.StrategyType == strategy);
        if (reconciliationStrategy == null)
        {
            return driftResult with
            {
                ReconciliationAttempted = false,
                ReconciliationSuccessful = false,
                ErrorMessage = $"Strategy '{strategy}' not implemented"
            };
        }

        // 3. Apply the strategy with timeout and retries
        bool success = false;
        string? errorMessage = null;
        int retryCount = 0;

        while (retryCount <= _options.MaxRetries)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_options.ReconciliationTimeout);

                success = await reconciliationStrategy.ApplyAsync(
                    instanceId,
                    driftResult.DesiredState!,
                    cts.Token);

                if (success)
                {
                    // Update actual state
                    await _stateStore.SetActualStateAsync(instanceId, driftResult.DesiredState!);
                    break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Outer cancellation, don't retry
                throw;
            }
            catch (OperationCanceledException)
            {
                // Timeout
                errorMessage = $"Reconciliation timed out after {_options.ReconciliationTimeout}";
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }

            retryCount++;
            if (retryCount <= _options.MaxRetries && !success)
            {
                await Task.Delay(_options.RetryDelay, cancellationToken);
            }
        }

        // 4. Record audit entry
        if (_options.EnableAuditLogging)
        {
            var action = success
                ? $"Successfully reconciled using {strategy} strategy"
                : $"Failed to reconcile using {strategy} strategy: {errorMessage}";

            await _stateStore.RecordReconciliationActionAsync(
                instanceId,
                action,
                true,
                DateTime.UtcNow);
        }

        return driftResult with
        {
            ReconciliationAttempted = true,
            ReconciliationSuccessful = success,
            StrategyUsed = strategy,
            ErrorMessage = success ? null : errorMessage
        };
    }

    private List<string> CompareConfigurations(Configuration desired, Configuration actual)
    {
        var differences = new List<string>();

        // Compare host
        if (desired.Host != actual.Host)
        {
            differences.Add($"Host: {actual.Host ?? "null"} → {desired.Host ?? "null"}");
        }

        // Compare port
        if (desired.Port != actual.Port)
        {
            differences.Add($"Port: {actual.Port?.ToString() ?? "null"} → {desired.Port?.ToString() ?? "null"}");
        }

        // Compare log level
        if (desired.LogLevel != actual.LogLevel)
        {
            differences.Add($"LogLevel: {actual.LogLevel ?? "null"} → {desired.LogLevel ?? "null"}");
        }

        // Compare sources count
        var desiredSourcesCount = desired.Sources?.Count ?? 0;
        var actualSourcesCount = actual.Sources?.Count ?? 0;
        if (desiredSourcesCount != actualSourcesCount)
        {
            differences.Add($"Sources count: {actualSourcesCount} → {desiredSourcesCount}");
        }

        // Compare queries count
        var desiredQueriesCount = desired.Queries?.Count ?? 0;
        var actualQueriesCount = actual.Queries?.Count ?? 0;
        if (desiredQueriesCount != actualQueriesCount)
        {
            differences.Add($"Queries count: {actualQueriesCount} → {desiredQueriesCount}");
        }

        // Compare reactions count
        var desiredReactionsCount = desired.Reactions?.Count ?? 0;
        var actualReactionsCount = actual.Reactions?.Count ?? 0;
        if (desiredReactionsCount != actualReactionsCount)
        {
            differences.Add($"Reactions count: {actualReactionsCount} → {desiredReactionsCount}");
        }

        // TODO: Deep comparison of sources, queries, reactions

        return differences;
    }
}
