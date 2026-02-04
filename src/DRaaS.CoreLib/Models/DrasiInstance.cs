namespace DRaaS.CoreLib.Models;

/// <summary>
/// Domain aggregate root representing a Drasi instance.
/// Includes both domain properties and infrastructure placement information.
/// </summary>
public record DrasiInstance
{
    public required string InstanceId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string[] Owners { get; init; }

    public required DateTime CreatedAt { get; init; }
    public required DateTime LastUpdatedAt { get; init; }

    /// <summary>
    /// History of state transitions for audit/tracking.
    /// Stack with most recent state on top for O(1) access.
    /// </summary>
    public Stack<DrasiInstanceState> StateHistory { get; init; } = new();

    /// <summary>
    /// Drasi-specific configuration (sources, queries, reactions).
    /// </summary>
    public DrasiConfiguration? Configuration { get; init; }

    /// <summary>
    /// User-defined metadata for the instance.
    /// </summary>
    public Dictionary<string, object?> MetaData { get; init; } = [];

    /// <summary>
    /// Infrastructure placement information from platform provider (null if not deployed).
    /// Contains runtime status, timestamps, and platform-specific metadata.
    /// </summary>
    public PlacementProviderRuntimeInfo? Placement { get; init; }

    /// <summary>
    /// Current instance status (unified domain + infrastructure state).
    /// Peeks top of state history stack for O(1) access.
    /// </summary>
    public Status CurrentStatus => StateHistory.Count > 0 ? StateHistory.Peek().Status : Status.Unknown;
}
