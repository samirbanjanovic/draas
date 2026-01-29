using DRaaS.Core.Models;

namespace DRaaS.Core.Messaging.Commands;

/// <summary>
/// Command to create a new instance.
/// </summary>
public record CreateInstanceCommand : Command
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public ServerConfiguration? ServerConfiguration { get; init; }
}

/// <summary>
/// Command to start an instance.
/// </summary>
public record StartInstanceCommand : Command
{
    public required string InstanceId { get; init; }
    public Configuration? Configuration { get; init; }
}

/// <summary>
/// Command to stop an instance.
/// </summary>
public record StopInstanceCommand : Command
{
    public required string InstanceId { get; init; }
}

/// <summary>
/// Command to restart an instance.
/// </summary>
public record RestartInstanceCommand : Command
{
    public required string InstanceId { get; init; }
}

/// <summary>
/// Command to delete an instance.
/// </summary>
public record DeleteInstanceCommand : Command
{
    public required string InstanceId { get; init; }
}
