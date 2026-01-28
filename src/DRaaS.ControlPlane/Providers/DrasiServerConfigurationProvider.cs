using DRaaS.ControlPlane.Models;
using DRaaS.ControlPlane.Services;
using Microsoft.AspNetCore.JsonPatch;
using YamlDotNet.Serialization;
using System.Collections.Concurrent;

namespace DRaaS.ControlPlane.Providers;

public class DrasiServerConfigurationProvider
    : IDrasiServerConfigurationProvider
{
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;
    private readonly IDrasiInstanceService _instanceService;
    private readonly ConcurrentDictionary<string, Configuration> _configurations = new();

    public DrasiServerConfigurationProvider(
        IDeserializer deserializer, 
        ISerializer serializer,
        IDrasiInstanceService instanceService)
    {
        _deserializer = deserializer;
        _serializer = serializer;
        _instanceService = instanceService;
    }

    public async Task InitializeConfigurationAsync(string instanceId, ServerConfiguration? serverConfig = null)
    {
        if (!await _instanceService.InstanceExistsAsync(instanceId))
        {
            throw new KeyNotFoundException($"Instance '{instanceId}' not found");
        }

        var config = new Configuration
        {
            Host = serverConfig?.Host ?? "0.0.0.0",
            Port = serverConfig?.Port ?? 8080,
            LogLevel = serverConfig?.LogLevel ?? "info",
            Sources = new List<Source>(),
            Queries = new List<Query>(),
            Reactions = new List<Reaction>()
        };

        _configurations.TryAdd(instanceId, config);

        // TODO: Write initial YAML to file storage (config/{instanceId}/server.yaml)
        var yaml = _serializer.Serialize(config);
    }

    public async Task<Configuration> GetConfigurationAsync(string instanceId)
    {
        if (!await _instanceService.InstanceExistsAsync(instanceId))
        {
            throw new KeyNotFoundException($"Instance '{instanceId}' not found");
        }

        if (!_configurations.TryGetValue(instanceId, out var config))
        {
            throw new InvalidOperationException($"Configuration for instance '{instanceId}' not initialized");
        }

        return config;
    }

    public async Task<Configuration> ApplyPatchAsync(string instanceId, JsonPatchDocument<Configuration> patchDocument)
    {
        if (!await _instanceService.InstanceExistsAsync(instanceId))
        {
            throw new KeyNotFoundException($"Instance '{instanceId}' not found");
        }

        if (!_configurations.TryGetValue(instanceId, out var config))
        {
            throw new InvalidOperationException($"Configuration for instance '{instanceId}' not initialized");
        }

        // Apply the JSON Patch document
        patchDocument.ApplyTo(config);

        _configurations[instanceId] = config;

        // Serialize to YAML for file storage
        var updatedYaml = _serializer.Serialize(config);
        // TODO: Write updatedYaml to file/storage (e.g., config/{instanceId}/server.yaml)

        return config;
    }

    public async Task PurgeConfigurationAsync(string instanceId)
    {
        if (!await _instanceService.InstanceExistsAsync(instanceId))
        {
            throw new KeyNotFoundException($"Instance '{instanceId}' not found");
        }

        var emptyConfig = new Configuration
        {
            Host = "0.0.0.0",
            Port = 8080,
            LogLevel = "info",
            Sources = new List<Source>(),
            Queries = new List<Query>(),
            Reactions = new List<Reaction>()
        };

        _configurations[instanceId] = emptyConfig;

        // TODO: Write to file storage
    }

    public async Task DeleteConfigurationAsync(string instanceId)
    {
        _configurations.TryRemove(instanceId, out _);
        // TODO: Delete configuration file from storage
    }

    // Server-specific Configuration Methods
    public async Task<ServerConfiguration> GetServerConfigurationAsync(string instanceId)
    {
        var config = await GetConfigurationAsync(instanceId);

        return new ServerConfiguration
        {
            Host = config.Host,
            Port = config.Port,
            LogLevel = config.LogLevel
        };
    }

    public async Task<ServerConfiguration> ApplyServerPatchAsync(
        string instanceId, 
        JsonPatchDocument<ServerConfiguration> patchDocument)
    {
        var config = await GetConfigurationAsync(instanceId);

        var serverConfig = new ServerConfiguration
        {
            Host = config.Host,
            Port = config.Port,
            LogLevel = config.LogLevel
        };

        patchDocument.ApplyTo(serverConfig);

        // Update the full configuration with patched server settings
        var updatedConfig = config with
        {
            Host = serverConfig.Host,
            Port = serverConfig.Port,
            LogLevel = serverConfig.LogLevel
        };

        _configurations[instanceId] = updatedConfig;

        // Serialize and save
        var updatedYaml = _serializer.Serialize(updatedConfig);
        // TODO: Write to file/storage

        return serverConfig;
    }

    public async Task<ServerConfiguration> UpdateHostAsync(string instanceId, string host)
    {
        var config = await GetConfigurationAsync(instanceId);
        var updatedConfig = config with { Host = host };

        _configurations[instanceId] = updatedConfig;

        var updatedYaml = _serializer.Serialize(updatedConfig);
        // TODO: Write to file/storage

        return new ServerConfiguration
        {
            Host = updatedConfig.Host,
            Port = updatedConfig.Port,
            LogLevel = updatedConfig.LogLevel
        };
    }

    public async Task<ServerConfiguration> UpdatePortAsync(string instanceId, int port)
    {
        var config = await GetConfigurationAsync(instanceId);
        var updatedConfig = config with { Port = port };

        _configurations[instanceId] = updatedConfig;

        var updatedYaml = _serializer.Serialize(updatedConfig);
        // TODO: Write to file/storage

        return new ServerConfiguration
        {
            Host = updatedConfig.Host,
            Port = updatedConfig.Port,
            LogLevel = updatedConfig.LogLevel
        };
    }

    public async Task<ServerConfiguration> UpdateLogLevelAsync(string instanceId, string logLevel)
    {
        var config = await GetConfigurationAsync(instanceId);
        var updatedConfig = config with { LogLevel = logLevel };

        _configurations[instanceId] = updatedConfig;

        var updatedYaml = _serializer.Serialize(updatedConfig);
        // TODO: Write to file/storage

        return new ServerConfiguration
        {
            Host = updatedConfig.Host,
            Port = updatedConfig.Port,
            LogLevel = updatedConfig.LogLevel
        };
    }
}
