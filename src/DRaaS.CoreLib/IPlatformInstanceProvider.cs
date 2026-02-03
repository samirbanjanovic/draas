using DRaaS.Core.Models;

namespace DRaaS.Core;

public interface IPlatformInstanceProvider
{
    PlatformInstanceProviderConfig Config { get; set; }
    PlatformInstanceProviderInfo Status { get; }
    Task<DrasiInstance> DeployInstanceAsync(DrasiInstance instance, CancellationToken cancellationToken);
    Task<DrasiInstance> StartInstanceAsync(string instanceId, CancellationToken cancellationToken);
    Task<DrasiInstance> StopInstanceAsync(string instanceId, CancellationToken cancellationToken);
    Task<DrasiInstance> GetInstanceRuntimeInfoAsync(string instanceId, CancellationToken cancellationToken);
}
