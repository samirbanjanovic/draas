using DRaaS.CoreLib.Models;
using System.Collections.Concurrent;

namespace DRaaS.CoreLib.Services.Impl;

public class LocalDrasiInstanceStorageService : IDrasiInstanceStorageService
{
    private readonly ConcurrentDictionary<string, DrasiInstance> _instances = new();

    public Task<DrasiInstance> SaveInstanceAsync(DrasiInstance instance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(instance.InstanceId);

        if (!_instances.TryAdd(instance.InstanceId, instance))
        {
            throw new InvalidOperationException(
                $"Instance '{instance.InstanceId}' already exists. Use UpdateInstanceAsync to modify existing instances.");
        }

        return Task.FromResult(instance);
    }

    public Task<DrasiInstance> UpdateInstanceAsync(DrasiInstance instance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(instance.InstanceId);

        if (!_instances.TryGetValue(instance.InstanceId, out _))
        {
            throw new KeyNotFoundException(
                $"Instance '{instance.InstanceId}' not found. Use SaveInstanceAsync to create new instances.");
        }

        _instances[instance.InstanceId] = instance;
        return Task.FromResult(instance);
    }

    public Task<DrasiInstance> GetInstanceAsync(string instanceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        if (!_instances.TryGetValue(instanceId, out var instance))
        {
            throw new KeyNotFoundException($"Instance '{instanceId}' not found.");
        }

        return Task.FromResult(instance);
    }

    public Task<IEnumerable<DrasiInstance>> GetInstancesByOwnerAsync(string ownerPrincipal, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerPrincipal);

        var instances = _instances.Values
            .Where(i => i.Owners.Contains(ownerPrincipal, StringComparer.OrdinalIgnoreCase))
            .ToList();

        return Task.FromResult<IEnumerable<DrasiInstance>>(instances);
    }

    public Task<IEnumerable<DrasiInstance>> GetInstancesByNameAsync(string instanceName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);

        var instances = _instances.Values
            .Where(i => i.Name.Equals(instanceName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Task.FromResult<IEnumerable<DrasiInstance>>(instances);
    }

    public Task<IEnumerable<DrasiInstance>> GetInstanceNamesByOwnerAsync(string ownerPrincipal, CancellationToken cancellationToken)
    {
        return GetInstancesByOwnerAsync(ownerPrincipal, cancellationToken);
    }

    public Task<IEnumerable<DrasiInstance>> GetAllInstancesAsync(CancellationToken cancellationToken)
    {
        var instances = _instances.Values.ToList();
        return Task.FromResult<IEnumerable<DrasiInstance>>(instances);
    }
}
