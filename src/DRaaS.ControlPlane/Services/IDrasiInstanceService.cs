using DRaaS.ControlPlane.Models;

namespace DRaaS.ControlPlane.Services;

public interface IDrasiInstanceService
{
    Task<DrasiInstance> CreateInstanceAsync(string name, string description = "", PlatformType? platformType = null);
    Task<DrasiInstance?> GetInstanceAsync(string instanceId);
    Task<IEnumerable<DrasiInstance>> GetAllInstancesAsync();
    Task<DrasiInstance> UpdateInstanceAsync(string instanceId, DrasiInstance instance);
    Task<bool> DeleteInstanceAsync(string instanceId);
    Task<DrasiInstance> UpdateInstanceStatusAsync(string instanceId, InstanceStatus status);
    Task<bool> InstanceExistsAsync(string instanceId);
}
