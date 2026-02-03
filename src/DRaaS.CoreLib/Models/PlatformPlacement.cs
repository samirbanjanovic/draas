namespace DRaaS.Core.Models;

public record PlatformPlacement
{    
    public required string InstanceId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string PlatformType { get; init; }
    public required DateTime PlacedAt { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = [];

}
