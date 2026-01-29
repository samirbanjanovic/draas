using DRaaS.Core.Models;

namespace DRaaS.Core.Messaging.Responses;

/// <summary>
/// Base response for instance command operations.
/// </summary>
public record InstanceCommandResponse : QueryResponse
{
    public required string InstanceId { get; init; }
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Response for StartInstanceCommand.
/// </summary>
public record StartInstanceResponse : InstanceCommandResponse
{
    public InstanceRuntimeInfo? RuntimeInfo { get; init; }
}

/// <summary>
/// Response for StopInstanceCommand.
/// </summary>
public record StopInstanceResponse : InstanceCommandResponse
{
    public InstanceRuntimeInfo? RuntimeInfo { get; init; }
}

/// <summary>
/// Response for RestartInstanceCommand.
/// </summary>
public record RestartInstanceResponse : InstanceCommandResponse
{
    public InstanceRuntimeInfo? RuntimeInfo { get; init; }
}

/// <summary>
/// Response for DeleteInstanceCommand.
/// </summary>
public record DeleteInstanceResponse : InstanceCommandResponse
{
    // No additional fields needed
}
