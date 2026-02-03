using DRaaS.Core.Models;

namespace DRaaS.Core;

public interface IPlatformOrchestrationService
{
    IPlatformInstanceProvider GetDefaultPlatformInstanceProvider();
    Task<PlatformPlacement> PlaceInstanceAsync(DrasiInstance instance, CancellationToken cancellationToken);
    Task<PlatformPlacement> GetInstancePlacementAsync(string instanceId, CancellationToken cancellationToken);
}

