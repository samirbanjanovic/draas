namespace DRaaS.Core;

internal interface IPlatformInstanceProviderFactory
{
    Task<IPlatformInstanceProvider> GetPlatformInstanceManagerAsync(string platformType);
    Task<IEnumerable<IPlatformInstanceProvider>> GetAllPlatformInstanceManagersAsync();
}
