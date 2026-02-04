namespace DRaaS.CoreLib.Models;

public record DrasiConfiguration
{
    public required string Host { get; init; }
    public required int Port { get; init; }
    public required string LogLevel { get; init; }
    public List<Source>? Sources { get; init; }
    public List<Query>? Queries { get; init; }
    public List<Reaction>? Reactions { get; init; }
}
