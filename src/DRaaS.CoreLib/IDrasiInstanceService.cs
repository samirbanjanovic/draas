using DRaaS.Core.Models;

namespace DRaaS.Core;

public interface IDrasiInstanceService
{
    DrasiInstance RegisterInstanceAsync(string ownerPrincipal,
                                        string name,
                                        string description,
                                        string platform,
                                        DrasiConfiguration config,
                                        Dictionary<string, string> metaData);

    DrasiInstance UpdateInstanceAsync(string instanceId,
                                      DrasiConfiguration config,
                                      Dictionary<string, string> metaData);

    DrasiInstance DeregisterInstanceAsync(string instanceId);

    DrasiInstance DeleteInstanceAsync(string instanceId);
    DrasiInstance GetInstanceByIdAsync(string instanceId);
    IEnumerable<DrasiInstance> GetInstancesByNameAsync(string instanceName);
    IEnumerable<DrasiInstance> GetInstancesByOwnerAsync(string ownerPrincipal);

}
