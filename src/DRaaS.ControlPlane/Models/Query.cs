namespace DRaaS.ControlPlane.Models;

public record Query
{
    public string? Id { get; init; }
    public string? QueryText { get; init; }
    public List<QuerySource>? Sources { get; init; }
}
