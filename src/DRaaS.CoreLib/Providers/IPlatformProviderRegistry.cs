using DRaaS.CoreLib.Models;

namespace DRaaS.CoreLib.Providers;

public interface IPlatformProviderRegistry
{
    IPlatformProvider DefaultProvider { get; }

    Task<IPlatformProvider> GetProviderAsync(
        string platformType, 
        CancellationToken cancellationToken = default);

    Task<IEnumerable<RegisteredPlatform>> GetProvidersAsync(
        bool availableOnly = false,
        CancellationToken cancellationToken = default);
}

