namespace DRaaS.CoreLib.Models;



public record DrasiInstance
{
    public required string InstanceId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string[] Owners { get; init; }

    public required DateTime CreatedAt { get; init; }
    public required DateTime LastUpdatedAt { get; init; }

    public List<DrasiInstanceState> StateHistory { get; init; } = [];
    public RuntimeInfo[]? RuntimeInfo { get; init; }
    public DrasiConfiguration? Configuration { get; init; }
    public Dictionary<string, object?> MetaData { get; init; } = [];
}
