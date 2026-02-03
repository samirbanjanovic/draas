using System;
using System.Collections.Generic;
using System.Text;

namespace DRaaS.CoreLib.Models;

/// <summary>
/// Runtime information returned by platform providers.
/// Contains platform-agnostic status and platform-specific metadata.
/// </summary>
public class PlacementProviderRuntimeInfo
{
    public required string InstanceId { get; init; }
    public required string PlatformType { get; init; }
    public required PlacementProviderRuntimeStatus Status { get; init; }
    public DateTime? DeployedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? StoppedAt { get; init; }

    /// <summary>
    /// Platform-specific metadata (e.g., ProcessId, ContainerId, PodName, resource limits).
    /// Values can be primitive types, collections, or complex objects.
    /// Ensure values are serializable if this info will be stored or transmitted.
    /// </summary>
    public Dictionary<string, object?> PlatformMetadata { get; init; } = [];
}
