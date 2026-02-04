using DRaaS.CoreLib.Services;
using DRaaS.CoreLib.Services.Impl;
using Microsoft.Extensions.DependencyInjection;

namespace DRaaS.CoreLib.Extensions;

/// <summary>
/// Extension methods for registering the unified instance orchestration service.
/// </summary>
public static class InstanceOrchestrationServiceExtensions
{
    /// <summary>
    /// Registers IInstanceOrchestrationService with the service collection.
    /// Requires IDrasiInstanceStorageService and IPlatformInstanceProviderFactory to be registered.
    /// </summary>
    public static IServiceCollection AddInstanceOrchestrationService(this IServiceCollection services)
    {
        services.AddScoped<IInstanceOrchestrationService, InstanceOrchestrationService>();
        return services;
    }
}
