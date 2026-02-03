using DRaaS.Core.Models;

namespace DRaaS.Core;

public interface IDrasiInstanceStorageService
{
    Task<DrasiInstance> SaveInstanceAsync(DrasiInstance instance, CancellationToken cancellationToken);
    Task<DrasiInstance> UpdateInstanceAsync(DrasiInstance instance, CancellationToken cancellationToken);
    Task<DrasiInstance> GetInstanceAsync(string instanceId, CancellationToken cancellationToken);
    Task<IEnumerable<DrasiInstance>> GetInstancesByOwnerAsync(string ownerPrincipal, CancellationToken cancellationToken);
    Task<IEnumerable<DrasiInstance>> GetInstancesByNameAsync(string instanceName, CancellationToken cancellationToken);
    Task<IEnumerable<DrasiInstance>> GetInstanceNamesByOwnerAsync(string ownerPrincipal, CancellationToken cancellationToken);
    Task<IEnumerable<DrasiInstance>> GetAllInstancesAsync(CancellationToken cancellationToken);
}
