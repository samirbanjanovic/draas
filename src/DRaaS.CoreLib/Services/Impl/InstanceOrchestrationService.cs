using DRaaS.CoreLib.Models;
using DRaaS.CoreLib.Providers;
using DRaaS.CoreLib.StateMachines;

namespace DRaaS.CoreLib.Services.Impl;

public class InstanceOrchestrationService : IInstanceOrchestrationService
{
    private readonly IDrasiInstanceStorageService _storageService;
    private readonly IPlatformProviderRegistry _providerRegistry;

    public InstanceOrchestrationService(
        IDrasiInstanceStorageService storageService,
        IPlatformProviderRegistry providerRegistry)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _providerRegistry = providerRegistry ?? throw new ArgumentNullException(nameof(providerRegistry));
    }

    public async Task<DrasiInstance> RegisterInstanceAsync(
        string name,
        string description,
        string[] owners,
        DrasiConfiguration configuration,
        Dictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(owners);
        ArgumentNullException.ThrowIfNull(configuration);

        var stateHistory = new Stack<DrasiInstanceStateTransition>();
        stateHistory.Push(new DrasiInstanceStateTransition
        {
            Status = DomainStatus.Registered,
            TimeStamp = DateTime.UtcNow,
            StateMetadata = new Dictionary<string, string> { ["Reason"] = "Initial registration" }
        });

        var instance = new DrasiInstance
        {
            InstanceId = Guid.NewGuid().ToString(),
            Name = name,
            Description = description,
            Owners = owners,
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
            Configuration = configuration,
            MetaData = metadata ?? new Dictionary<string, object?>(),
            StateHistory = stateHistory,
            Placement = null
        };

        instance = await AddDomainStateTransitionAsync(instance, DomainStatus.Configured,
            new Dictionary<string, string> { ["Reason"] = "Configuration provided" },
            cancellationToken);

        return await _storageService.SaveInstanceAsync(instance, cancellationToken);
    }

    public async Task<DrasiInstance> DeployInstanceAsync(
        string name,
        string description,
        string[] owners,
        DrasiConfiguration configuration,
        string? platformType = null,
        Dictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var instance = await RegisterInstanceAsync(
            name,
            description,
            owners,
            configuration,
            metadata,
            cancellationToken);

        return await DeployInstanceAsync(instance.InstanceId, platformType, cancellationToken);
    }

    public async Task<DrasiInstance> DeployInstanceAsync(
        string instanceId,
        string? platformType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        var instance = await _storageService.GetInstanceAsync(instanceId, cancellationToken);

        if (!instance.Status.HasValidConfiguration())
        {
            throw new InvalidOperationException(
                $"Instance {instanceId} cannot be deployed - configuration is not valid. Current status: {instance.Status}");
        }

        if (instance.Placement != null)
        {
            throw new InvalidOperationException(
                $"Instance {instanceId} is already deployed to {instance.Placement.PlatformType}. Current runtime status: {instance.RuntimeStatus}");
        }

        var provider = platformType != null
            ? await _providerRegistry.GetProviderAsync(platformType, cancellationToken)
            : _providerRegistry.DefaultProvider;

        if (!provider.IsAvailable)
        {
            throw new InvalidOperationException($"Platform provider '{provider.PlatformType}' is not available.");
        }

        PlacementProviderRuntimeInfo runtimeInfo;
        try
        {
            runtimeInfo = await provider.DeployInstanceAsync(
                instance.InstanceId,
                instance.Name,
                instance.Configuration,
                cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to deploy instance {instanceId} to {provider.PlatformType}: {ex.Message}", ex);
        }

        runtimeInfo = runtimeInfo with { LastSyncedAt = DateTime.UtcNow };

        instance.Placement = runtimeInfo;
        instance.LastUpdatedAt = DateTime.UtcNow;

        return await _storageService.SaveInstanceAsync(instance, cancellationToken);
    }

    public async Task<DrasiInstance> StartInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        var instance = await _storageService.GetInstanceAsync(instanceId, cancellationToken);

        if (instance.Placement == null)
        {
            throw new InvalidOperationException($"Instance {instanceId} is not deployed. Deploy first before starting.");
        }

        var provider = await _providerRegistry.GetProviderAsync(
            instance.Placement.PlatformType,
            cancellationToken);

        PlacementProviderRuntimeInfo runtimeInfo;
        try
        {
            runtimeInfo = await provider.StartInstanceAsync(instanceId, cancellationToken);
        }
        catch (InvalidStateTransitionException ex)
        {
            throw new InvalidOperationException(
                $"Cannot start instance {instanceId}: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to start instance {instanceId}: {ex.Message}", ex);
        }

        instance.Placement = runtimeInfo with { LastSyncedAt = DateTime.UtcNow };
        instance.LastUpdatedAt = DateTime.UtcNow;

        return await _storageService.SaveInstanceAsync(instance, cancellationToken);
    }

    public async Task<DrasiInstance> StopInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        var instance = await _storageService.GetInstanceAsync(instanceId, cancellationToken);

        if (instance.Placement == null)
        {
            throw new InvalidOperationException($"Instance {instanceId} is not deployed.");
        }

        var provider = await _providerRegistry.GetProviderAsync(
            instance.Placement.PlatformType,
            cancellationToken);

        PlacementProviderRuntimeInfo runtimeInfo;
        try
        {
            runtimeInfo = await provider.StopInstanceAsync(instanceId, cancellationToken);
        }
        catch (InvalidStateTransitionException ex)
        {
            throw new InvalidOperationException(
                $"Cannot stop instance {instanceId}: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to stop instance {instanceId}: {ex.Message}", ex);
        }

        instance.Placement = runtimeInfo with { LastSyncedAt = DateTime.UtcNow };
        instance.LastUpdatedAt = DateTime.UtcNow;

        return await _storageService.SaveInstanceAsync(instance, cancellationToken);
    }

    public async Task<DrasiInstance> RestartInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        var instance = await StopInstanceAsync(instanceId, cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        return await StartInstanceAsync(instanceId, cancellationToken);
    }

    public async Task DeleteInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        var instance = await _storageService.GetInstanceAsync(instanceId, cancellationToken);

        if (instance.Placement != null)
        {
            if (instance.RuntimeStatus == PlacementProviderRuntimeStatus.Running ||
                instance.RuntimeStatus == PlacementProviderRuntimeStatus.Starting)
            {
                try
                {
                    await StopInstanceAsync(instanceId, cancellationToken);
                    instance = await _storageService.GetInstanceAsync(instanceId, cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to stop instance {instanceId} before deletion: {ex.Message}");
                }
            }

            var provider = await _providerRegistry.GetProviderAsync(
                instance.Placement!.PlatformType,
                cancellationToken);

            try
            {
                await provider.DeleteInstanceAsync(instanceId, cancellationToken);
                instance.Placement = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to delete infrastructure for {instanceId}: {ex.Message}");
            }
        }

        instance = await AddDomainStateTransitionAsync(instance, DomainStatus.Deregistered,
            new Dictionary<string, string> { ["Reason"] = "Instance deleted" },
            cancellationToken);

        await _storageService.SaveInstanceAsync(instance, cancellationToken);
    }


    public async Task<DrasiInstance> UpdateInstanceConfigurationAsync(
        string instanceId,
        DrasiConfiguration configuration,
        Dictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentNullException.ThrowIfNull(configuration);

        var instance = await _storageService.GetInstanceAsync(instanceId, cancellationToken);

        if (!instance.Status.CanModifyConfiguration())
        {
            throw new InvalidOperationException(
                $"Instance {instanceId} cannot modify configuration while {instance.Status}.");
        }

        if (instance.Placement != null)
        {
            var infraStatus = instance.RuntimeStatus!.Value;
            if (infraStatus != PlacementProviderRuntimeStatus.Stopped &&
                infraStatus != PlacementProviderRuntimeStatus.Deployed)
            {
                throw new InvalidOperationException(
                    $"Instance {instanceId} must be stopped before updating configuration. Current infrastructure state: {infraStatus}");
            }
        }

        instance.Configuration = configuration;
        instance.MetaData = metadata ?? instance.MetaData;
        instance.LastUpdatedAt = DateTime.UtcNow;

        instance = await AddDomainStateTransitionAsync(instance, DomainStatus.Configured,
            new Dictionary<string, string> { ["Reason"] = "Configuration updated" },
            cancellationToken);

        return await _storageService.SaveInstanceAsync(instance, cancellationToken);
    }

    public async Task<DrasiInstance> UpdateInstanceMetadataAsync(
        string instanceId,
        Dictionary<string, object?> metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentNullException.ThrowIfNull(metadata);

        var instance = await _storageService.GetInstanceAsync(instanceId, cancellationToken);

        instance.MetaData = metadata;
        instance.LastUpdatedAt = DateTime.UtcNow;

        return await _storageService.UpdateInstanceAsync(instance, cancellationToken);
    }

    public async Task<DrasiInstance> GetInstanceAsync(
        string instanceId,
        bool refreshState = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        var instance = await _storageService.GetInstanceAsync(instanceId, cancellationToken);

        if (refreshState && instance.Placement != null)
        {
            try
            {
                var provider = await _providerRegistry.GetProviderAsync(
                    instance.Placement.PlatformType,
                    cancellationToken);

                var runtimeInfo = await provider.GetInstanceInfoAsync(instanceId, cancellationToken);

                instance.Placement = runtimeInfo with { LastSyncedAt = DateTime.UtcNow };
                instance.LastUpdatedAt = DateTime.UtcNow;

                instance = await _storageService.SaveInstanceAsync(instance, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to refresh infrastructure state for {instanceId}: {ex.Message}");
            }
        }

        return instance;
    }

    public async Task<IEnumerable<DrasiInstance>> GetInstancesByOwnerAsync(
        string ownerPrincipal,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerPrincipal);
        return await _storageService.GetInstancesByOwnerAsync(ownerPrincipal, cancellationToken);
    }

    public async Task<IEnumerable<DrasiInstance>> GetAllInstancesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _storageService.GetAllInstancesAsync(cancellationToken);
    }

    public async Task<IEnumerable<DrasiInstance>> GetInstancesByStatusAsync(
        DomainStatus status,
        CancellationToken cancellationToken = default)
    {
        var allInstances = await _storageService.GetAllInstancesAsync(cancellationToken);
        return allInstances.Where(i => i.Status == status);
    }

    public async Task<IEnumerable<PlatformDetails>> GetAvailablePlatformsAsync(
        CancellationToken cancellationToken = default)
    {
        var registered = await _providerRegistry.GetProvidersAsync(availableOnly: false, cancellationToken);

        return registered.Select(r => new PlatformDetails
        {
            PlatformType = r.Info.PlatformType,
            IsAvailable = r.Info.IsAvailable,
            IsDefault = r.IsDefault,
            InstanceCount = r.Info.InstanceCount,
            Labels = r.Info.Labels,
            Metadata = r.Info.Metadata
        });
    }

    public async Task<PlatformDetails> GetDefaultPlatformAsync(
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        var defaultProvider = _providerRegistry.DefaultProvider;
        var info = defaultProvider.GetPlatformInfo();

        return new PlatformDetails
        {
            PlatformType = info.PlatformType,
            IsAvailable = info.IsAvailable,
            IsDefault = true,
            InstanceCount = info.InstanceCount,
            Labels = info.Labels,
            Metadata = info.Metadata
        };
    }

    private async Task<DrasiInstance> AddDomainStateTransitionAsync(
        DrasiInstance instance,
        DomainStatus newStatus,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        var newState = new DrasiInstanceStateTransition
        {
            Status = newStatus,
            TimeStamp = DateTime.UtcNow,
            StateMetadata = metadata
        };

        instance.StateHistory.Push(newState);
        instance.LastUpdatedAt = DateTime.UtcNow;

        return await _storageService.SaveInstanceAsync(instance, cancellationToken);
    }
}
