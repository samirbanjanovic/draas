namespace DRaaS.CoreLib.Models;

public record PlacementProviderRuntimeInfo
{
    public required string InstanceId { get; init; }
    public required string PlatformType { get; init; }
    public required PlacementProviderRuntimeStatus Status { get; init; }
    public DateTime? DeployedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? StoppedAt { get; init; }
    public DateTime? LastSyncedAt { get; init; }
    public Dictionary<string, object?> PlatformMetadata { get; init; } = [];
}

public enum PlacementProviderRuntimeStatus
{
    Unknown = 0,
    Deploying,
    Deployed,
    Starting,
    Running,
    Stopping,
    Stopped,
    Failed,
    Deleted
}