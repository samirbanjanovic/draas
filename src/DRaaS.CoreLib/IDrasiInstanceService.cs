using DRaaS.Core.Models;

namespace DRaaS.Core;

public interface IDrasiInstanceService
{
    Task<DrasiInstance> RegisterInstanceAsync(string ownerPrincipal,
                                        string name,
                                        string description,
                                        string platform,
                                        DrasiConfiguration config,
                                        Dictionary<string, string> metaData);

    Task<DrasiInstance> UpdateInstanceAsync(string instanceId,
                                      DrasiConfiguration config,
                                      Dictionary<string, string> metaData);

    Task<DrasiInstance> DeregisterInstanceAsync(string instanceId);

    Task<DrasiInstance> DeleteInstanceAsync(string instanceId);
    Task<DrasiInstance> GetInstanceByIdAsync(string instanceId);
    Task<IEnumerable<DrasiInstance>> GetInstancesByNameAsync(string instanceName);
    Task<IEnumerable<DrasiInstance>> GetInstancesByOwnerAsync(string ownerPrincipal);

}
