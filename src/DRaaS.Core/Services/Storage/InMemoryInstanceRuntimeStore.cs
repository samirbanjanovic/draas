using System.Collections.Concurrent;
using DRaaS.Core.Models;

namespace DRaaS.Core.Services.Storage;

public class InMemoryInstanceRuntimeStore : IInstanceRuntimeStore
{
    private readonly ConcurrentDictionary<string, InstanceRuntimeInfo> _store = new();

    public Task SaveAsync(InstanceRuntimeInfo runtimeInfo)
    {
        _store.AddOrUpdate(
            runtimeInfo.InstanceId,
            runtimeInfo,
            (_, _) => runtimeInfo);

        return Task.CompletedTask;
    }

    public Task<InstanceRuntimeInfo?> GetAsync(string instanceId)
    {
        _store.TryGetValue(instanceId, out var runtimeInfo);
        return Task.FromResult(runtimeInfo);
    }

    public Task<IEnumerable<InstanceRuntimeInfo>> GetAllAsync()
    {
        return Task.FromResult<IEnumerable<InstanceRuntimeInfo>>(_store.Values.ToList());
    }

    public Task<IEnumerable<InstanceRuntimeInfo>> GetByPlatformAsync(PlatformType platformType)
    {
        var platformTypeString = platformType.ToString();
        var filtered = _store.Values
            .Where(info => info.RuntimeMetadata.TryGetValue("PlatformType", out var pt) && pt == platformTypeString)
            .ToList();

        return Task.FromResult<IEnumerable<InstanceRuntimeInfo>>(filtered);
    }

    public Task<bool> DeleteAsync(string instanceId)
    {
        return Task.FromResult(_store.TryRemove(instanceId, out _));
    }

    public Task<bool> ExistsAsync(string instanceId)
    {
        return Task.FromResult(_store.ContainsKey(instanceId));
    }
}
