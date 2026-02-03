using DRaaS.Core.Models;

namespace DRaaS.Core;

public interface IDrasiInstanceStorageService
{
    Task<DrasiInstance> SaveInstanceAsync(DrasiInstance instance);
    Task<DrasiInstance> UpdateInstanceAsync(DrasiInstance instance);
    Task<DrasiInstance> GetInstanceAsync(string instanceId);
    Task<IEnumerable<DrasiInstance>> GetInstancesByOwnerAsync(string ownerPrincipal);
    Task<IEnumerable<DrasiInstance>> GetInstancesByNameAsync(string instanceName);
    Task<IEnumerable<DrasiInstance>> GetInstanceNamesByOwnerAsync(string ownerPrincipal);
    Task<IEnumerable<DrasiInstance>> GetAllInstancesAsync();
}
