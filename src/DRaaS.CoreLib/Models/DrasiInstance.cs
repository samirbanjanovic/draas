namespace DRaaS.CoreLib.Models;

/// <summary>
/// Domain aggregate root representing a Drasi instance.
/// Includes both domain properties and infrastructure placement information.
/// </summary>
public class DrasiInstance
{
    public required string InstanceId { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string[] Owners { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime LastUpdatedAt { get; set; }
    public Stack<DrasiInstanceState> StateHistory { get; set; } = new();

    public DrasiConfiguration? Configuration { get; set; }

    public Dictionary<string, object?> MetaData { get; set; } = [];
    
    public PlacementProviderRuntimeInfo? Placement { get; set; }

    public Status CurrentStatus => StateHistory.Count > 0 ? StateHistory.Peek().Status : Status.Unknown;
}
