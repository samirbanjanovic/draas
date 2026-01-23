using DRaaS.ControlPlane.Models;
using Microsoft.AspNetCore.JsonPatch;
using YamlDotNet.Serialization;

namespace DRaaS.ControlPlane.Providers;

public class DrasiServerConfigurationProvider
    : IDrasiServerConfigurationProvider
{
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;

    public DrasiServerConfigurationProvider(IDeserializer deserializer, ISerializer serializer)
    {
        _deserializer = deserializer;
        _serializer = serializer;
    }

    public Task<Configuration> GetConfigurationAsync()
    {
        // read existing configuration from storage
        // hardcoded configuration for testing purposes
        var existingConfigurationYaml =
@"sources:
  - kind: mock
    id: test-source
    autoStart: true

queries:
  - id: my-query
    queryText: ""MATCH (n:Node) RETURN n""
    sources:
      - sourceId: test-source

reactions:
  - kind: log
    id: log-output
    queries: [my-query]";

        var configuration = _deserializer.Deserialize<Configuration>(existingConfigurationYaml);
        return Task.FromResult(configuration);
    }

    public async Task<Configuration> ApplyPatchAsync(JsonPatchDocument<Configuration> patchDocument)
    {
        // Get existing configuration
        var config = await GetConfigurationAsync();

        // Apply the JSON Patch document
        patchDocument.ApplyTo(config);

        // Serialize back to YAML
        var updatedYaml = _serializer.Serialize(config);

        // TODO: Write updatedYaml to file/storage (e.g., config/server.yaml)
        // For now, just return the patched configuration

        return config;
    }

    public Task PurgeConfigurationAsync()
    {
        throw new NotImplementedException();
    }
}
