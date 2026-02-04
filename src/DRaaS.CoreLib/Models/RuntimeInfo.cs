namespace DRaaS.CoreLib.Models;



public record RuntimeInfo
{
    public required string PlatformType { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? StoppedAt { get; init; }
    public required Dictionary<string, object?> RuntimeMetadata { get; init; } = [];
}
