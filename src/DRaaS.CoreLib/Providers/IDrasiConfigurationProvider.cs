using DRaaS.CoreLib.Models;

namespace DRaaS.CoreLib.Providers;

public interface IDrasiConfigurationProvider
{
    string GenerateConfiguration(
        string instanceId, 
        DrasiConfiguration configuration,
        IDictionary<string, object?>? additionalSettings = null);
}
