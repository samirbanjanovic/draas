using DRaaS.Core.Models;
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

    /// <summary>
    /// Optional deployment-specific metadata (e.g., resource requests, labels, tags).
    /// Values can be primitive types, collections, or complex objects.
    /// </summary>
    public Dictionary<string, object?> DeploymentMetadata { get; init; } = []; 
}
