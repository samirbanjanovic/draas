using DRaaS.WebApi.Models.Common;

namespace DRaaS.WebApi.Models.Requests;

/// <summary>
/// Request DTO for creating a new instance.
/// </summary>
public record CreateInstanceRequest
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string[] Owners { get; init; }
    public required ConfigurationDto Configuration { get; init; }
    public string? PlatformType { get; init; }
    public Dictionary<string, object?>? Metadata { get; init; }
}

/// <summary>
/// Configuration DTO used in create requests.
/// Uses arrays for straightforward input.
/// </summary>
public record ConfigurationDto
{
    public required string Host { get; init; }
    public required int Port { get; init; }
    public required string LogLevel { get; init; }
    public List<SourceDto>? Sources { get; init; }
    public List<QueryDto>? Queries { get; init; }
    public List<ReactionDto>? Reactions { get; init; }
}
