using DRaaS.Core.Models;

namespace DRaaS.Core.Services.Reconciliation;

/// <summary>
/// Result of drift detection between desired state and actual state for an instance.
/// Contains information about whether drift exists and what differences were found.
/// </summary>
public record DriftDetectionResult
{
    /// <summary>
    /// The instance identifier that was checked for drift.
    /// </summary>
    public string InstanceId { get; init; } = string.Empty;

    /// <summary>
    /// Indicates whether configuration drift was detected.
    /// True if desired state differs from actual state.
    /// </summary>
    public bool HasDrift { get; init; }

    /// <summary>
    /// The desired configuration state (what should be running).
    /// </summary>
    public Configuration? DesiredState { get; init; }

    /// <summary>
    /// The actual configuration state (what is currently running).
    /// </summary>
    public Configuration? ActualState { get; init; }

    /// <summary>
    /// List of specific differences between desired and actual state.
    /// Examples: "Host changed: 127.0.0.1 -> 0.0.0.0", "Added source: postgresql-db"
    /// </summary>
    public List<string> Differences { get; init; } = new();

    /// <summary>
    /// Timestamp when drift detection was performed.
    /// </summary>
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Indicates whether reconciliation was attempted.
    /// </summary>
    public bool ReconciliationAttempted { get; init; }

    /// <summary>
    /// Indicates whether reconciliation was successful (if attempted).
    /// </summary>
    public bool? ReconciliationSuccessful { get; init; }

    /// <summary>
    /// The strategy used for reconciliation (if attempted).
    /// </summary>
    public ReconciliationStrategy? StrategyUsed { get; init; }

    /// <summary>
    /// Error message if reconciliation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Additional metadata about the drift detection or reconciliation.
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// Creates a result indicating no drift was detected.
    /// </summary>
    public static DriftDetectionResult NoDrift(string instanceId, Configuration state) => new()
    {
        InstanceId = instanceId,
        HasDrift = false,
        DesiredState = state,
        ActualState = state,
        Differences = new List<string>(),
        DetectedAt = DateTime.UtcNow
    };

    /// <summary>
    /// Creates a result indicating drift was detected.
    /// </summary>
    public static DriftDetectionResult WithDrift(
        string instanceId,
        Configuration desiredState,
        Configuration actualState,
        List<string> differences) => new()
    {
        InstanceId = instanceId,
        HasDrift = true,
        DesiredState = desiredState,
        ActualState = actualState,
        Differences = differences,
        DetectedAt = DateTime.UtcNow
    };
}
