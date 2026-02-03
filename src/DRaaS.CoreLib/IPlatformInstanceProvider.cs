using DRaaS.Core.Models;

namespace DRaaS.Core;

public record InstanceRuntimeInfo();

public interface IPlatformInstanceProvider
{
    PlatformInstanceManagerConfig Config { get; set; }

    PlatformInstanceManagerInfo Status { get; }

    DrasiInstance DeployInstanceAsync(DrasiInstance instance);

    DrasiInstance StartInstanceAsync(string instanceId);

    DrasiInstance StopInstanceAsync(string instanceId);

    DrasiInstance GetInstanceRuntimeInfoAsync(string instanceId);
}
