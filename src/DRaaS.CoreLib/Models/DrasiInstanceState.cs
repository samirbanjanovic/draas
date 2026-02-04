namespace DRaaS.CoreLib.Models;

public record DrasiInstanceState
{
    public required Status Status { get; init; } = Status.Unknown;
    public required DateTime TimeStamp { get; init; } = DateTime.Now;
    public required Dictionary<string, string> StateMetadata { get; init; } = [];
}
