using DRaaS.Core.Models;

namespace DRaaS.Core;

public interface IPlatformOrchestrationService
{
    IPlatformInstanceProvider GetDefaultPlatformInstanceManager();
    PlatformPlacement PlaceInstance(DrasiInstance instance);
    PlatformPlacement GetInstancePlacement(string instanceId);
}

