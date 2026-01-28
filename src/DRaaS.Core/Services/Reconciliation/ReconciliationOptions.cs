namespace DRaaS.Core.Services.Reconciliation;

/// <summary>
/// Configuration options for the reconciliation service.
/// Controls behavior of automatic drift detection and reconciliation.
/// </summary>
public class ReconciliationOptions
{
    /// <summary>
    /// Interval between automatic reconciliation checks.
    /// Default: 30 seconds.
    /// Set to TimeSpan.Zero to disable periodic reconciliation.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Default reconciliation strategy to use when strategy is not explicitly specified.
    /// Default: Restart (simplest strategy).
    /// </summary>
    public ReconciliationStrategy DefaultStrategy { get; set; } = ReconciliationStrategy.Restart;

    /// <summary>
    /// Enables or disables automatic reconciliation.
    /// When true, reconciliation runs on PollingInterval and responds to configuration change events.
    /// When false, reconciliation must be triggered manually via API.
    /// Default: true.
    /// </summary>
    public bool EnableAutoReconciliation { get; set; } = true;

    /// <summary>
    /// Enables or disables event-driven reconciliation.
    /// When true, reconciliation is triggered immediately when configuration changes.
    /// When false, only periodic reconciliation occurs (if enabled).
    /// Default: true.
    /// </summary>
    public bool EnableEventDrivenReconciliation { get; set; } = true;

    /// <summary>
    /// Maximum number of reconciliation retries on failure before giving up.
    /// Default: 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts when reconciliation fails.
    /// Default: 10 seconds.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Maximum number of instances to reconcile concurrently during ReconcileAllInstancesAsync.
    /// Default: 5 (prevents overwhelming the system).
    /// </summary>
    public int MaxConcurrentReconciliations { get; set; } = 5;

    /// <summary>
    /// Whether to record reconciliation actions in the audit trail.
    /// Default: true (recommended for compliance and debugging).
    /// </summary>
    public bool EnableAuditLogging { get; set; } = true;

    /// <summary>
    /// Timeout for a single reconciliation operation.
    /// If reconciliation takes longer, it will be cancelled.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan ReconciliationTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to reconcile instances that are in Error status.
    /// When true, reconciliation will attempt to fix instances in error state.
    /// When false, error instances are skipped.
    /// Default: true (self-healing behavior).
    /// </summary>
    public bool ReconcileErrorInstances { get; set; } = true;

    /// <summary>
    /// Whether to reconcile instances that are currently Stopped.
    /// When true, stopped instances will have their configuration updated (but remain stopped).
    /// When false, stopped instances are skipped.
    /// Default: false (don't touch stopped instances).
    /// </summary>
    public bool ReconcileStoppedInstances { get; set; } = false;
}
