using DRaaS.Core.Models;
using Microsoft.AspNetCore.JsonPatch;

namespace DRaaS.Core.Messaging.Commands;

/// <summary>
/// Command to initialize configuration for an instance.
/// </summary>
public record InitializeConfigurationCommand : Command
{
    public required string InstanceId { get; init; }
    public ServerConfiguration? ServerConfiguration { get; init; }
}

/// <summary>
/// Command to update full configuration.
/// </summary>
public record UpdateConfigurationCommand : Command
{
    public required string InstanceId { get; init; }
    public required Configuration Configuration { get; init; }
}

/// <summary>
/// Command to apply JSON Patch to configuration.
/// </summary>
public record PatchConfigurationCommand : Command
{
    public required string InstanceId { get; init; }
    public required string PatchDocumentJson { get; init; }
}

/// <summary>
/// Command to delete configuration for an instance.
/// </summary>
public record DeleteConfigurationCommand : Command
{
    public required string InstanceId { get; init; }
}
