using System;
using System.Collections.Generic;
using System.Text;

namespace DRaaS.Core;


public record PlatformInstanceManagerConfig
{
    public const string SectionName = "PlatformInstanceManagerOptions";
    public string? SomeOption { get; set; }
}

public record PlatformInstanceManagerInfo
{
    public string Status { get; set; } = "Suspended";
}

public record InstanceRuntimeInfo();

public interface IPlatformInstanceManager
{
    PlatformInstanceManagerConfig Config { get; set; }

    PlatformInstanceManagerInfo Status { get; }

    InstanceRuntimeInfo StartInstanceAsync(string instanceId);

    InstanceRuntimeInfo StopInstanceAsync(string instanceId);

    InstanceRuntimeInfo GetInstanceRuntimeInfoAsync(string instanceId);
}
