namespace DRaaS.ControlPlane.Models;

public record DrasiInstance
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public PlatformType PlatformType { get; init; } = PlatformType.Process;
    public DateTime CreatedAt { get; init; }
    public DateTime? LastModifiedAt { get; init; }
    public InstanceStatus Status { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}

public enum InstanceStatus
{
    Created,
    Running,
    Stopped,
    Error
}
