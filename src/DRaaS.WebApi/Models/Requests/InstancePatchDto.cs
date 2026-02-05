using DRaaS.WebApi.Models.Common;

namespace DRaaS.WebApi.Models.Requests;

/// <summary>
/// DTO target for JSON Patch operations.
/// Uses dictionaries keyed by ID for precise array element addressing.
/// </summary>
public class InstancePatchDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string[]? Owners { get; set; }
    public ConfigurationPatchDto? Configuration { get; set; }
    public Dictionary<string, object?>? Metadata { get; set; }
}

/// <summary>
/// Configuration DTO optimized for JSON Patch operations.
/// Arrays are represented as dictionaries keyed by ID for ID-based addressing.
/// Example: /configuration/sources/my-source-id instead of /configuration/sources/0
/// Contains only user-configurable instance settings (not hosting details).
/// </summary>
public class ConfigurationPatchDto
{
    /// <summary>
    /// Sources keyed by Source.Id for ID-based patch addressing.
    /// </summary>
    public Dictionary<string, SourceDto>? Sources { get; set; }

    /// <summary>
    /// Queries keyed by Query.Id for ID-based patch addressing.
    /// </summary>
    public Dictionary<string, QueryDto>? Queries { get; set; }

    /// <summary>
    /// Reactions keyed by Reaction.Id for ID-based patch addressing.
    /// </summary>
    public Dictionary<string, ReactionDto>? Reactions { get; set; }
}
