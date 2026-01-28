namespace DRaaS.Core.Models;

public record InstanceRuntimeInfo
{
    public string InstanceId { get; init; } = string.Empty;
    public InstanceStatus Status { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? StoppedAt { get; init; }
    public string? ContainerId { get; init; }
    public string? ProcessId { get; init; }
    public string? PodName { get; init; }
    public string? Namespace { get; init; }
    public Dictionary<string, string> RuntimeMetadata { get; init; } = new();
    public string? ErrorMessage { get; init; }
}
