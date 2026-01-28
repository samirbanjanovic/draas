using DRaaS.ControlPlane.Models;

namespace DRaaS.ControlPlane.Services;

public interface IPlatformOrchestratorService
{
    /// <summary>
    /// Selects the appropriate platform for a new instance based on configuration and load
    /// </summary>
    Task<PlatformType> SelectPlatformAsync();

    /// <summary>
    /// Allocates resources (host, port) for a new instance on the specified platform
    /// </summary>
    Task<ServerConfiguration> AllocateResourcesAsync(PlatformType platformType);

    /// <summary>
    /// Releases resources when an instance is deleted
    /// </summary>
    Task ReleaseResourcesAsync(string instanceId, ServerConfiguration configuration);

    /// <summary>
    /// Gets the configured default platform type
    /// </summary>
    PlatformType GetDefaultPlatform();
}
