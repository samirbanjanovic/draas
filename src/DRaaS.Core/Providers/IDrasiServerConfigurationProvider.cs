using DRaaS.Core.Models;
using Microsoft.AspNetCore.JsonPatch;

namespace DRaaS.Core.Providers;

public interface IDrasiServerConfigurationProvider
{
    // Initialize configuration for a new instance
    Task InitializeConfigurationAsync(string instanceId, ServerConfiguration? serverConfig = null);

    // Full Configuration Management
    Task<Configuration> GetConfigurationAsync(string instanceId);
    Task<Configuration> ApplyPatchAsync(string instanceId, JsonPatchDocument<Configuration> patchDocument);
    Task PurgeConfigurationAsync(string instanceId);

    // Server-specific Configuration Management
    Task<ServerConfiguration> GetServerConfigurationAsync(string instanceId);
    Task<ServerConfiguration> ApplyServerPatchAsync(string instanceId, JsonPatchDocument<ServerConfiguration> patchDocument);
    Task<ServerConfiguration> UpdateHostAsync(string instanceId, string host);
    Task<ServerConfiguration> UpdatePortAsync(string instanceId, int port);
    Task<ServerConfiguration> UpdateLogLevelAsync(string instanceId, string logLevel);

    // Delete configuration when instance is deleted
    Task DeleteConfigurationAsync(string instanceId);
}
