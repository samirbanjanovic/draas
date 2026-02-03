using DRaaS.Core.Models;

namespace DRaaS.Core;

public interface IPlatformInstanceProvider
{
    PlatformInstanceManagerConfig Config { get; set; }
    PlatformInstanceManagerInfo Status { get; }
    Task<DrasiInstance> DeployInstanceAsync(DrasiInstance instance);
    Task<DrasiInstance> StartInstanceAsync(string instanceId);
    Task<DrasiInstance> StopInstanceAsync(string instanceId);
    Task<DrasiInstance> GetInstanceRuntimeInfoAsync(string instanceId);
}
