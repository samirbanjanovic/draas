namespace DRaaS.Core.Models;

public record PlatformPlacement
{
    public required IPlatformInstanceProvider InstanceManager { get; init; }
    public required string InstanceId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string PlatformType { get; init; }
    public required DateTime PlacemedAt { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = [];

}
