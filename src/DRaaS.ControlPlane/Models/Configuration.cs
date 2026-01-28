namespace DRaaS.ControlPlane.Models;

public record Configuration
{
    public string? Host { get; init; }
    public int? Port { get; init; }
    public string? LogLevel { get; init; }
    public List<Source>? Sources { get; init; }
    public List<Query>? Queries { get; init; }
    public List<Reaction>? Reactions { get; init; }
}
