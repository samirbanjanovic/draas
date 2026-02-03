using DRaaS.Core.Models;

namespace DRaaS.Core;

public interface IPlatformOrchestrationService
{
    IPlatformInstanceProvider GetDefaultPlatformInstanceManager();
    Task<PlatformPlacement> PlaceInstanceAsync(DrasiInstance instance);
    Task<PlatformPlacement> GetInstancePlacementAsync(string instanceId);
}

