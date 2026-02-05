namespace DRaaS.WebApi.Models.Common;

/// <summary>
/// DTO representing a reaction configuration.
/// </summary>
public record ReactionDto
{
    public required string Kind { get; init; }
    public required string Id { get; init; }
    public List<string>? Queries { get; init; }
}
