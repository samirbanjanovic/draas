namespace DRaaS.ControlPlane.Models;

public record Source
{
    public string? Kind { get; init; }
    public string? Id { get; init; }
    public bool? AutoStart { get; init; }
}
