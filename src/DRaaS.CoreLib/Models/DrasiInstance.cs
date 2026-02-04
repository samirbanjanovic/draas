using System.Diagnostics.CodeAnalysis;

namespace DRaaS.CoreLib.Models;

public class DrasiInstance
{
    public required string InstanceId { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string[] Owners { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime LastUpdatedAt { get; set; }    
    public required DrasiConfiguration Configuration { get; set; }

    public Dictionary<string, object?> MetaData { get; set; } = [];
    public Stack<DrasiInstanceStateTransition> StateHistory { get; set; } = new();

    public DomainStatus Status => StateHistory.Count > 0 
        ? StateHistory.Peek().Status 
        : DomainStatus.Registered;

    public PlacementProviderRuntimeInfo? Placement { get; set; }
    public PlacementProviderRuntimeStatus? RuntimeStatus => Placement?.Status;

    public bool IsFullyOperational => 
        Status == DomainStatus.Configured && 
        RuntimeStatus == PlacementProviderRuntimeStatus.Running;

    public bool IsReadyForDeployment => 
        Status == DomainStatus.Configured && 
        Placement == null;

    public bool NeedsCleanup => 
        Status == DomainStatus.Deregistered && 
        Placement != null && 
        RuntimeStatus != PlacementProviderRuntimeStatus.Deleted;
}
