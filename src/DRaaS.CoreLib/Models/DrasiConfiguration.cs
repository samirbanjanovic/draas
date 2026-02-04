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

public record QuerySource
{
    public required string SourceId { get; init; }
}


public record Query
{
    public required string Id { get; init; }
    public required string QueryText { get; init; }
    public List<QuerySource>? Sources { get; init; }
}

public record Reaction
{
    public required string Kind { get; init; }
    public required string Id { get; init; }
    public List<string>? Queries { get; init; }
}

public record Source
{
    public required string Kind { get; init; }
    public required string Id { get; init; }
    public required bool AutoStart { get; init; } = false;
}

