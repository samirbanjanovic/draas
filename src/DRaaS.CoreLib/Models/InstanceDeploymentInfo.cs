using DRaaS.CoreLib.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DRaaS.CoreLib.Models;

/// <summary>
/// Information required to deploy an instance to a platform provider.
/// Contains only what the provider needs to create the runtime environment.
/// </summary>
public class InstanceDeploymentInfo
{
    public required string InstanceId { get; init; }
    public required string Name { get; init; }
    public required DrasiConfiguration Configuration { get; init; }
    public Dictionary<string, object?> DeploymentMetadata { get; init; } = []; 
}
