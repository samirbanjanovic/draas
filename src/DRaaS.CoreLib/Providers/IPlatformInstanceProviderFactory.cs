namespace DRaaS.CoreLib.Providers;

public interface IPlatformInstanceProviderFactory
{
    IPlatformInstanceProvider DefaultProvider { get; }
    Task<IPlatformInstanceProvider> GetPlatformInstanceProviderAsync(string platformType, CancellationToken cancellationToken);
    Task<IEnumerable<IPlatformInstanceProvider>> GetAllPlatformInstanceProvidersAsync(CancellationToken cancellationToken);
}
