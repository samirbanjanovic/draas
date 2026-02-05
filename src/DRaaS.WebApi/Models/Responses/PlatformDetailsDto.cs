namespace DRaaS.WebApi.Models.Responses;

/// <summary>
/// Response DTO for platform information.
/// </summary>
public record PlatformDetailsDto
{
    public required string PlatformType { get; init; }
    public required bool IsAvailable { get; init; }
    public required bool IsDefault { get; init; }
    public int? InstanceCount { get; init; }
    public Dictionary<string, string> Labels { get; init; } = new();
    public Dictionary<string, object?> Metadata { get; init; } = new();
}
