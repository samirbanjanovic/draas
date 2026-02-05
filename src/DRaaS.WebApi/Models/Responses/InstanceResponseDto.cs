using DRaaS.WebApi.Models.Common;

namespace DRaaS.WebApi.Models.Responses;

/// <summary>
/// Response DTO for instance data.
/// Abstracts domain model from API consumers.
/// </summary>
public record InstanceResponseDto
{
    public required string InstanceId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string[] Owners { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime LastUpdatedAt { get; init; }
    
    /// <summary>
    /// Domain status (Registered, Configured, ConfigurationError, Deregistered).
    /// </summary>
    public required string Status { get; init; }
    
    /// <summary>
    /// Runtime/infrastructure status (Running, Stopped, etc.) - null if not deployed.
    /// </summary>
    public string? RuntimeStatus { get; init; }
    
    public required ConfigurationResponseDto Configuration { get; init; }
    public required Dictionary<string, object?> Metadata { get; init; }
    public PlacementResponseDto? Placement { get; init; }
    
    /// <summary>
    /// True when domain status is Configured AND runtime status is Running.
    /// </summary>
    public bool IsFullyOperational { get; init; }
    
    /// <summary>
    /// True when domain status is Configured AND not yet deployed.
    /// </summary>
    public bool IsReadyForDeployment { get; init; }
    
    /// <summary>
    /// True when deregistered but infrastructure still exists.
    /// </summary>
    public bool NeedsCleanup { get; init; }
}

/// <summary>
/// Configuration response DTO. Uses arrays for easy consumption.
/// </summary>
public record ConfigurationResponseDto
{
    public required string Host { get; init; }
    public required int Port { get; init; }
    public required string LogLevel { get; init; }
    public List<SourceDto>? Sources { get; init; }
    public List<QueryDto>? Queries { get; init; }
    public List<ReactionDto>? Reactions { get; init; }
}
