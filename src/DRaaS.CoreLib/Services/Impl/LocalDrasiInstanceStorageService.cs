using DRaaS.CoreLib.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DRaaS.CoreLib.Services.Impl;

public class LocalDrasiInstanceStorageService
    : IDrasiInstanceStorageService
{
    public Task<IEnumerable<DrasiInstance>> GetAllInstancesAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<DrasiInstance> GetInstanceAsync(string instanceId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<DrasiInstance>> GetInstanceNamesByOwnerAsync(string ownerPrincipal, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<DrasiInstance>> GetInstancesByNameAsync(string instanceName, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<DrasiInstance>> GetInstancesByOwnerAsync(string ownerPrincipal, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<DrasiInstance> SaveInstanceAsync(DrasiInstance instance, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<DrasiInstance> UpdateInstanceAsync(DrasiInstance instance, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
