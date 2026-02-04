using DRaaS.CoreLib.Models;
using DRaaS.CoreLib.Providers;

namespace DRaaS.CoreLib.Services;

public interface IPlatformOrchestrationService
{
    IPlatformInstanceProvider GetDefaultPlatformInstanceProvider();
    Task<PlatformPlacement> PlaceInstanceAsync(DrasiInstance instance, CancellationToken cancellationToken);
    Task<PlatformPlacement> GetInstancePlacementAsync(string instanceId, CancellationToken cancellationToken);
}

