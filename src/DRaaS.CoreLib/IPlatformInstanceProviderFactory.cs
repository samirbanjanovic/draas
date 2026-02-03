namespace DRaaS.Core;

public interface IPlatformInstanceProviderFactory
{
    Task<IPlatformInstanceProvider> GetPlatformInstanceProviderAsync(string platformType, CancellationToken cancellationToken);
    Task<IEnumerable<IPlatformInstanceProvider>> GetAllPlatformInstanceProvidersAsync(CancellationToken cancellationToken);
}
