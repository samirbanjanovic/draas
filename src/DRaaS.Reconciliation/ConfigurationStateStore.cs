using DRaaS.Core.Models;
using DRaaS.Core.Services.Reconciliation;
using System.Collections.Concurrent;

namespace DRaaS.Reconciliation;

/// <summary>
/// Manages desired state and actual state for configuration reconciliation.
/// Retrieves desired state from ControlPlane API and tracks actual state locally.
/// </summary>
public class ConfigurationStateStore : IConfigurationStateStore
{
    private readonly IReconciliationApiClient _apiClient;
    private readonly ILogger<ConfigurationStateStore> _logger;
    private readonly ConcurrentDictionary<string, Configuration> _actualState = new();
    private readonly ConcurrentDictionary<string, List<ReconciliationAuditEntry>> _auditLog = new();

    public ConfigurationStateStore(
        IReconciliationApiClient apiClient,
        ILogger<ConfigurationStateStore> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<Configuration?> GetDesiredStateAsync(string instanceId)
    {
        // Desired state comes from ControlPlane (source of truth)
        try
        {
            return await _apiClient.GetConfigurationAsync(instanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to get desired configuration for instance {InstanceId}",
                instanceId);
            return null;
        }
    }

    public Task<Configuration?> GetActualStateAsync(string instanceId)
    {
        // Actual state is what was last successfully applied
        _actualState.TryGetValue(instanceId, out var config);
        return Task.FromResult(config);
    }

    public Task SetActualStateAsync(string instanceId, Configuration configuration)
    {
        // Update actual state after successful reconciliation
        _actualState.AddOrUpdate(instanceId, configuration, (_, _) => configuration);
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<string>> GetAllInstanceIdsAsync()
    {
        // Get all instances from ControlPlane
        try
        {
            var instances = await _apiClient.GetAllInstancesAsync();
            return instances.Select(i => i.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all instances from ControlPlane");
            return Enumerable.Empty<string>();
        }
    }

    public Task RecordReconciliationActionAsync(
        string instanceId,
        string action,
        bool driftDetected,
        DateTime timestamp)
    {
        var entry = new ReconciliationAuditEntry
        {
            InstanceId = instanceId,
            Action = action,
            DriftDetected = driftDetected,
            Timestamp = timestamp
        };

        _auditLog.AddOrUpdate(
            instanceId,
            new List<ReconciliationAuditEntry> { entry },
            (_, list) =>
            {
                list.Add(entry);
                // Keep only last 100 entries per instance
                if (list.Count > 100)
                {
                    list.RemoveAt(0);
                }
                return list;
            });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the reconciliation audit trail for an instance.
    /// </summary>
    public Task<IEnumerable<ReconciliationAuditEntry>> GetAuditTrailAsync(string instanceId)
    {
        _auditLog.TryGetValue(instanceId, out var entries);
        return Task.FromResult<IEnumerable<ReconciliationAuditEntry>>(entries ?? new List<ReconciliationAuditEntry>());
    }
}

/// <summary>
/// Represents a single entry in the reconciliation audit log.
/// </summary>
public record ReconciliationAuditEntry
{
    public string InstanceId { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public bool DriftDetected { get; init; }
    public DateTime Timestamp { get; init; }
}
