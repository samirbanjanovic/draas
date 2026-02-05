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
/// Contains only user-configurable instance settings (not hosting details).
/// </summary>
public record ConfigurationDto
{
    /// <summary>
    /// Data sources for the instance.
    /// </summary>
    public List<SourceDto>? Sources { get; init; }

    /// <summary>
    /// Continuous queries to execute.
    /// </summary>
    public List<QueryDto>? Queries { get; init; }

    /// <summary>
    /// Reactions triggered by query results.
    /// </summary>
    public List<ReactionDto>? Reactions { get; init; }
}
