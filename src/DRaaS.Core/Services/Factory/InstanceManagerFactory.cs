using DRaaS.Core.Models;
using DRaaS.Core.Providers;
using DRaaS.Core.Providers.InstanceManagers;

namespace DRaaS.Core.Services.Factory;

public class InstanceManagerFactory : IInstanceManagerFactory
{
    private readonly Dictionary<PlatformType, IDrasiServerInstanceManager> _managers;
    private readonly PlatformType _defaultPlatform;

    public InstanceManagerFactory(IEnumerable<IDrasiServerInstanceManager> managers, PlatformType? defaultPlatform = null)
    {
        _managers = managers.ToDictionary(
            m => Enum.Parse<PlatformType>(m.PlatformType), 
            m => m);

        _defaultPlatform = defaultPlatform ?? PlatformType.Process;

        if (!_managers.ContainsKey(_defaultPlatform))
        {
            throw new InvalidOperationException($"Default platform '{_defaultPlatform}' not registered");
        }
    }

    public IDrasiServerInstanceManager GetManager(PlatformType platformType)
    {
        if (!_managers.TryGetValue(platformType, out var manager))
        {
            throw new ArgumentException($"No instance manager registered for platform '{platformType}'", nameof(platformType));
        }

        return manager;
    }

    public IEnumerable<IDrasiServerInstanceManager> GetAllManagers()
    {
        return _managers.Values;
    }

    public IDrasiServerInstanceManager GetDefaultManager()
    {
        return _managers[_defaultPlatform];
    }
}
