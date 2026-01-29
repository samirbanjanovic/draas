using DRaaS.Core.Models;

namespace DRaaS.Core.Messaging.Events;

/// <summary>
/// Event published when an instance is created.
/// </summary>
public record InstanceCreatedEvent : Event
{
    public required string InstanceId { get; init; }
    public required string Name { get; init; }
    public PlatformType PlatformType { get; init; }
}

/// <summary>
/// Event published when an instance is started.
/// </summary>
public record InstanceStartedEvent : Event
{
    public required string InstanceId { get; init; }
}

/// <summary>
/// Event published when an instance is stopped.
/// </summary>
public record InstanceStoppedEvent : Event
{
    public required string InstanceId { get; init; }
}

/// <summary>
/// Event published when an instance status changes.
/// </summary>
public record InstanceStatusChangedEvent : Event
{
    public required string InstanceId { get; init; }
    public InstanceStatus OldStatus { get; init; }
    public InstanceStatus NewStatus { get; init; }
    public string? Source { get; init; }
}

/// <summary>
/// Event published when an instance is deleted.
/// </summary>
public record InstanceDeletedEvent : Event
{
    public required string InstanceId { get; init; }
}
