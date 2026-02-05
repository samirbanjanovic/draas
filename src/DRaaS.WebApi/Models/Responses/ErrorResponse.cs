namespace DRaaS.WebApi.Models.Responses;

/// <summary>
/// Standardized error response DTO.
/// </summary>
public record ErrorResponse
{
    /// <summary>
    /// Primary error message.
    /// </summary>
    public required string Error { get; init; }
    
    /// <summary>
    /// Additional error details (optional).
    /// </summary>
    public string? Detail { get; init; }
    
    /// <summary>
    /// Validation errors by field name (optional).
    /// </summary>
    public Dictionary<string, string[]>? ValidationErrors { get; init; }
}
