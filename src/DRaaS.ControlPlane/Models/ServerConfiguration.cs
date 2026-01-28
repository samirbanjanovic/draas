namespace DRaaS.ControlPlane.Models;

public record ServerConfiguration
{
    public string? Host { get; init; }
    public int? Port { get; init; }
    public string? LogLevel { get; init; }
}
