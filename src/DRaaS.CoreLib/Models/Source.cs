namespace DRaaS.Core.Models;

public record Source
{
    public required string Kind { get; init; }
    public required string Id { get; init; }
    public required bool AutoStart { get; init; } = false;
}

