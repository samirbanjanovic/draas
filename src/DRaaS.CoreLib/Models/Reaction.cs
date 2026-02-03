namespace DRaaS.Core.Models;

public record Reaction
{
    public required string Kind { get; init; }
    public required string Id { get; init; }
    public List<string>? Queries { get; init; }
}
