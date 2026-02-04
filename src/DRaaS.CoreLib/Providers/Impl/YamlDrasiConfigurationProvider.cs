using DRaaS.CoreLib.Models;
using System.Text;

namespace DRaaS.CoreLib.Providers.Impl;

/// <summary>
/// Generates YAML configuration for Drasi server instances.
/// </summary>
public class YamlDrasiConfigurationProvider : IDrasiConfigurationProvider
{
    /// <summary>
    /// Generates YAML configuration content for a Drasi instance.
    /// </summary>
    /// <param name="instanceId">Unique identifier for the instance</param>
    /// <param name="configuration">Drasi configuration settings</param>
    /// <param name="additionalSettings">Optional settings (e.g., "DefaultLogLevel")</param>
    /// <returns>YAML configuration as a string</returns>
    public string GenerateConfiguration(
        string instanceId,
        DrasiConfiguration configuration,
        IDictionary<string, object?>? additionalSettings = null)
    {
        var defaultLogLevel = GetDefaultLogLevel(additionalSettings);
        
        var yamlConfig = new StringBuilder();
        
        // Core settings
        yamlConfig.AppendLine($"id: {instanceId}");
        yamlConfig.AppendLine($"host: {configuration.Host}");
        yamlConfig.AppendLine($"port: {configuration.Port}");
        yamlConfig.AppendLine($"logLevel: {configuration.LogLevel ?? defaultLogLevel}");
        yamlConfig.AppendLine("persistConfig: true");
        yamlConfig.AppendLine("persistIndex: false");
        yamlConfig.AppendLine();

        // Sources
        AppendSources(yamlConfig, configuration);
        
        // Queries
        AppendQueries(yamlConfig, configuration);
        
        // Reactions
        AppendReactions(yamlConfig, configuration);

        return yamlConfig.ToString();
    }

    private static string GetDefaultLogLevel(IDictionary<string, object?>? additionalSettings)
    {
        if (additionalSettings?.TryGetValue("DefaultLogLevel", out var logLevel) == true 
            && logLevel is string logLevelStr)
        {
            return logLevelStr;
        }
        
        return "Info";
    }

    private static void AppendSources(StringBuilder yamlConfig, DrasiConfiguration configuration)
    {
        if (configuration.Sources?.Count > 0)
        {
            yamlConfig.AppendLine("sources:");
            foreach (var source in configuration.Sources)
            {
                yamlConfig.AppendLine($"  - kind: {source.Kind}");
                yamlConfig.AppendLine($"    id: {source.Id}");
                yamlConfig.AppendLine($"    autoStart: {source.AutoStart.ToString().ToLowerInvariant()}");
            }
            yamlConfig.AppendLine();
        }
        else
        {
            yamlConfig.AppendLine("sources: []");
            yamlConfig.AppendLine();
        }
    }

    private static void AppendQueries(StringBuilder yamlConfig, DrasiConfiguration configuration)
    {
        if (configuration.Queries?.Count > 0)
        {
            yamlConfig.AppendLine("queries:");
            foreach (var query in configuration.Queries)
            {
                yamlConfig.AppendLine($"  - id: {query.Id}");
                
                if (!string.IsNullOrWhiteSpace(query.QueryText))
                {
                    yamlConfig.AppendLine("    query: |");
                    var queryLines = query.QueryText.Split('\n');
                    foreach (var line in queryLines)
                    {
                        yamlConfig.AppendLine($"      {line}");
                    }
                }
                
                yamlConfig.AppendLine("    sources:");
                if (query.Sources?.Count > 0)
                {
                    foreach (var source in query.Sources)
                    {
                        yamlConfig.AppendLine($"      - sourceId: {source.SourceId}");
                    }
                }
            }
            yamlConfig.AppendLine();
        }
        else
        {
            yamlConfig.AppendLine("queries: []");
            yamlConfig.AppendLine();
        }
    }

    private static void AppendReactions(StringBuilder yamlConfig, DrasiConfiguration configuration)
    {
        if (configuration.Reactions?.Count > 0)
        {
            yamlConfig.AppendLine("reactions:");
            foreach (var reaction in configuration.Reactions)
            {
                yamlConfig.AppendLine($"  - kind: {reaction.Kind}");
                yamlConfig.AppendLine($"    id: {reaction.Id}");
                
                if (reaction.Queries?.Count > 0)
                {
                    yamlConfig.Append("    queries: [");
                    yamlConfig.Append(string.Join(", ", reaction.Queries));
                    yamlConfig.AppendLine("]");
                }
                else
                {
                    yamlConfig.AppendLine("    queries: []");
                }
            }
            yamlConfig.AppendLine();
        }
        else
        {
            yamlConfig.AppendLine("reactions: []");
            yamlConfig.AppendLine();
        }
    }
}
