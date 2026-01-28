using DRaaS.Core.Models;

namespace DRaaS.ControlPlane.DTOs;

public record CreateInstanceRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public ServerConfiguration? ServerConfiguration { get; init; }
}
