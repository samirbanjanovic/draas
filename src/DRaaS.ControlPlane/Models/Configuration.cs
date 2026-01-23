namespace DRaaS.ControlPlane.Models;

public record Configuration
{
    public List<Source>? Sources { get; init; }
    public List<Query>? Queries { get; init; }
    public List<Reaction>? Reactions { get; init; }
}
