namespace DRaaS.Core.Models;

public record Reaction
{
    public string? Kind { get; init; }
    public string? Id { get; init; }
    public List<string>? Queries { get; init; }
}
