namespace DRaaS.WebApi.Models.Common;

/// <summary>
/// DTO representing a data source configuration.
/// </summary>
public record SourceDto
{
    public required string Kind { get; init; }
    public required string Id { get; init; }
    public bool AutoStart { get; init; }
}
