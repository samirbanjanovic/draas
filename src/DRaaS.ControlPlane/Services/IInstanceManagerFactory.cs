using DRaaS.ControlPlane.Models;
using DRaaS.ControlPlane.Providers;

namespace DRaaS.ControlPlane.Services;

public interface IInstanceManagerFactory
{
    /// <summary>
    /// Gets an instance manager for the specified platform type
    /// </summary>
    IDrasiServerInstanceManager GetManager(PlatformType platformType);

    /// <summary>
    /// Gets all available instance managers
    /// </summary>
    IEnumerable<IDrasiServerInstanceManager> GetAllManagers();

    /// <summary>
    /// Gets the default instance manager
    /// </summary>
    IDrasiServerInstanceManager GetDefaultManager();
}
