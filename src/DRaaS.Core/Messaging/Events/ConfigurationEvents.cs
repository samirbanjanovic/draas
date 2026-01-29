using DRaaS.Core.Models;

namespace DRaaS.Core.Messaging.Events;

/// <summary>
/// Event published when configuration is initialized for an instance.
/// </summary>
public record ConfigurationInitializedEvent : Event
{
    public required string InstanceId { get; init; }
}

/// <summary>
/// Event published when configuration is updated.
/// </summary>
public record ConfigurationUpdatedEvent : Event
{
    public required string InstanceId { get; init; }
}

/// <summary>
/// Event published when configuration is deleted.
/// </summary>
public record ConfigurationDeletedEvent : Event
{
    public required string InstanceId { get; init; }
}
