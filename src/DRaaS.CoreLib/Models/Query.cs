namespace DRaaS.CoreLib.Models;

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

