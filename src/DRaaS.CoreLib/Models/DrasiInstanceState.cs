namespace DRaaS.CoreLib.Models;

public record DrasiInstanceState
{
    public required Status Status { get; init; } = Status.Unknown;
    public required DateTime TimeStamp { get; init; } = DateTime.Now;
    public required Dictionary<string, string> StateMetadata { get; init; } = [];
}

public enum Status
{
    /// <summary>
    /// Status unknown or not set.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Instance registered in domain, no infrastructure deployment.
    /// </summary>
    Registered,

    /// <summary>
    /// Instance deregistered (soft delete / inactive).
    /// </summary>
    Deregistered,

    /// <summary>
    /// Infrastructure is being created (deployment in progress).
    /// Maps to PlacementProviderRuntimeStatus.Deploying.
    /// </summary>
    Creating,

    /// <summary>
    /// Infrastructure created and ready to start.
    /// Maps to PlacementProviderRuntimeStatus.Deployed.
    /// </summary>
    Created,

    /// <summary>
    /// Infrastructure starting up.
    /// Maps to PlacementProviderRuntimeStatus.Starting.
    /// </summary>
    Starting,

    /// <summary>
    /// Infrastructure running and operational.
    /// Maps to PlacementProviderRuntimeStatus.Running.
    /// </summary>
    Running,

    /// <summary>
    /// Infrastructure stopping.
    /// Maps to PlacementProviderRuntimeStatus.Stopping.
    /// </summary>
    Stopping,

    /// <summary>
    /// Infrastructure stopped but still deployed.
    /// Maps to PlacementProviderRuntimeStatus.Stopped.
    /// </summary>
    Stopped,

    /// <summary>
    /// Instance being deleted.
    /// </summary>
    Deleting,

    /// <summary>
    /// Instance deleted.
    /// Maps to PlacementProviderRuntimeStatus.Deleted.
    /// </summary>
    Deleted,

    /// <summary>
    /// Error or failure occurred.
    /// Maps to PlacementProviderRuntimeStatus.Failed.
    /// </summary>
    Error,
}
