using DRaaS.Core.Models;

namespace DRaaS.CoreLib.Services;

public interface IDrasiInstanceService
{
    Task<DrasiInstance> RegisterInstanceAsync(string ownerPrincipal,
                                        string name,
                                        string description,
                                        string platform,
                                        DrasiConfiguration config,
                                        Dictionary<string, string> metaData,
                                        CancellationToken cancellationToken);

    Task<DrasiInstance> UpdateInstanceAsync(string instanceId,
                                      DrasiConfiguration config,
                                      Dictionary<string, string> metaData,
                                      CancellationToken cancellationToken);

    Task<DrasiInstance> DeregisterInstanceAsync(string instanceId, CancellationToken cancellationToken);

    Task<DrasiInstance> DeleteInstanceAsync(string instanceId, CancellationToken cancellationToken);
    Task<DrasiInstance> GetInstanceByIdAsync(string instanceId, CancellationToken cancellationToken);
    Task<IEnumerable<DrasiInstance>> GetInstancesByNameAsync(string instanceName, CancellationToken cancellationToken);
    Task<IEnumerable<DrasiInstance>> GetInstancesByOwnerAsync(string ownerPrincipal, CancellationToken cancellationToken);

}
