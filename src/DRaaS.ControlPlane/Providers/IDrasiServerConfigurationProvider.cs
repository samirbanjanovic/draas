using DRaaS.ControlPlane.Models;
using Microsoft.AspNetCore.JsonPatch;

namespace DRaaS.ControlPlane.Providers;

public interface IDrasiServerConfigurationProvider
{
    Task<Configuration> GetConfigurationAsync();
    Task<Configuration> ApplyPatchAsync(JsonPatchDocument<Configuration> patchDocument);
    Task PurgeConfigurationAsync();
}
