namespace DRaaS.WebApi.Models.Common;

/// <summary>
/// DTO representing a query configuration.
/// </summary>
public record QueryDto
{
    public required string Id { get; init; }
    public required string QueryText { get; init; }
    public List<QuerySourceDto>? Sources { get; init; }
}

/// <summary>
/// DTO representing a source reference within a query.
/// </summary>
public record QuerySourceDto
{
    public required string SourceId { get; init; }
}
