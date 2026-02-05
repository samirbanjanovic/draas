namespace DRaaS.WebApi.Models.Responses;

/// <summary>
/// Response DTO for placement/infrastructure information.
/// </summary>
public record PlacementResponseDto
{
    public required string InstanceId { get; init; }
    public required string PlatformType { get; init; }
    public required string Status { get; init; }
    public DateTime? DeployedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? StoppedAt { get; init; }
    public DateTime? LastSyncedAt { get; init; }
    public required Dictionary<string, object?> PlatformMetadata { get; init; }
}
