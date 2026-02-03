using DRaaS.Core.Models;

namespace DRaaS.Core;

public interface IDrasiInstanceStorageService
{
    DrasiInstance SaveInstanceAsync(DrasiInstance instance);
    DrasiInstance UpdateInstanceAsync(DrasiInstance instance);
    DrasiInstance GetInstanceAsync(string instanceId);
    IEnumerable<DrasiInstance> GetInstancesByOwnerAsync(string ownerPrincipal);
    IEnumerable<DrasiInstance> GetInstancesByNameAsync(string instanceName);
    IEnumerable<DrasiInstance> GetInstanceNamesByOwnerAsync(string ownerPrincipal);
    IEnumerable<DrasiInstance> GetAllInstancesAsync();
}
