namespace DRaaS.Core;

internal interface IPlatformInstanceProviderFactory
{
    IPlatformInstanceProvider GetPlatformInstanceManager(string platformType);
    IEnumerable<IPlatformInstanceProvider> GetAllPlatformInstanceManagers();
}
