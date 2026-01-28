using System.Collections.Concurrent;
using DRaaS.ControlPlane.Models;

namespace DRaaS.ControlPlane.Services;

public class DrasiInstanceService : IDrasiInstanceService
{
    private readonly ConcurrentDictionary<string, DrasiInstance> _instances = new();
    private readonly IPlatformOrchestratorService _orchestrator;

    public DrasiInstanceService(IPlatformOrchestratorService orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task<DrasiInstance> CreateInstanceAsync(string name, string description = "", PlatformType? platformType = null)
    {
        var instanceId = Guid.NewGuid().ToString();

        // If platform not specified, let orchestrator select it
        var selectedPlatform = platformType ?? await _orchestrator.SelectPlatformAsync();

        var instance = new DrasiInstance
        {
            Id = instanceId,
            Name = name,
            Description = description,
            PlatformType = selectedPlatform,
            CreatedAt = DateTime.UtcNow,
            Status = InstanceStatus.Created,
            Metadata = new Dictionary<string, string>()
        };

        _instances.TryAdd(instanceId, instance);
        return instance;
    }

    public Task<DrasiInstance?> GetInstanceAsync(string instanceId)
    {
        _instances.TryGetValue(instanceId, out var instance);
        return Task.FromResult(instance);
    }

    public Task<IEnumerable<DrasiInstance>> GetAllInstancesAsync()
    {
        return Task.FromResult<IEnumerable<DrasiInstance>>(_instances.Values.ToList());
    }

    public Task<DrasiInstance> UpdateInstanceAsync(string instanceId, DrasiInstance instance)
    {
        if (!_instances.ContainsKey(instanceId))
        {
            throw new KeyNotFoundException($"Instance with ID '{instanceId}' not found");
        }

        var updatedInstance = instance with
        {
            Id = instanceId,
            LastModifiedAt = DateTime.UtcNow
        };

        _instances[instanceId] = updatedInstance;
        return Task.FromResult(updatedInstance);
    }

    public Task<bool> DeleteInstanceAsync(string instanceId)
    {
        return Task.FromResult(_instances.TryRemove(instanceId, out _));
    }

    public Task<DrasiInstance> UpdateInstanceStatusAsync(string instanceId, InstanceStatus status)
    {
        if (!_instances.TryGetValue(instanceId, out var instance))
        {
            throw new KeyNotFoundException($"Instance with ID '{instanceId}' not found");
        }

        var updatedInstance = instance with
        {
            Status = status,
            LastModifiedAt = DateTime.UtcNow
        };

        _instances[instanceId] = updatedInstance;
        return Task.FromResult(updatedInstance);
    }

    public Task<bool> InstanceExistsAsync(string instanceId)
    {
        return Task.FromResult(_instances.ContainsKey(instanceId));
    }
}
