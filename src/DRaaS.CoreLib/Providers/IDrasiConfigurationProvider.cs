using DRaaS.CoreLib.Models;

namespace DRaaS.CoreLib.Providers;

/// <summary>
/// Provides functionality to generate Drasi server configuration content.
/// Different implementations can generate different formats (YAML, JSON, etc.).
/// </summary>
public interface IDrasiConfigurationProvider
{
    /// <summary>
    /// Generates configuration content for a Drasi instance.
    /// </summary>
    /// <param name="instanceId">Unique identifier for the instance</param>
    /// <param name="configuration">Drasi configuration settings</param>
    /// <param name="additionalSettings">Provider-specific settings (e.g., default log level)</param>
    /// <returns>Configuration content as a string</returns>
    string GenerateConfiguration(
        string instanceId, 
        DrasiConfiguration configuration,
        IDictionary<string, object?>? additionalSettings = null);
}
