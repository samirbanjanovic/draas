namespace DRaaS.CoreLib.Models;

public record PlatformInfo
{
    public required string PlatformType { get; init; }
    public required bool IsAvailable { get; init; }

    public int? InstanceCount { get; init; }

    public Dictionary<string, string> Labels { get; init; } = new();
    public Dictionary<string, object?> Metadata { get; init; } = new();
}

public record RegisteredPlatform(bool IsDefault, PlatformInfo Info);
