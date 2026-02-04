using DRaaS.CoreLib.Models;
using DRaaS.CoreLib.Providers;

namespace DRaaS.CoreLib.Services.Impl;

/// <summary>
/// Unified orchestration service that manages complete instance lifecycle.
/// Coordinates domain persistence (IDrasiInstanceStorageService) and infrastructure placement (IPlatformInstanceProviderFactory).
/// Maintains DrasiInstance as single source of truth with embedded placement.
/// </summary>
public class InstanceOrchestrationService : IInstanceOrchestrationService
{
    private readonly IDrasiInstanceStorageService _storageService;
    private readonly IPlatformInstanceProviderFactory _providerFactory;

    public InstanceOrchestrationService(
        IDrasiInstanceStorageService storageService,
        IPlatformInstanceProviderFactory providerFactory)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
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

        var stateHistory = new Stack<DrasiInstanceState>();
        stateHistory.Push(new DrasiInstanceState
        {
            Status = Status.Registered,
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

        return await _storageService.SaveInstanceAsync(instance, cancellationToken);
    }

    public async Task<DrasiInstance> DeployInstanceAsync(
        string instanceId,
        string? platformType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        var instance = await _storageService.GetInstanceAsync(instanceId, cancellationToken);

        if (instance.CurrentStatus != Status.Registered && instance.CurrentStatus != Status.Created)
        {
            throw new InvalidOperationException(
                $"Instance {instanceId} cannot be deployed from state {instance.CurrentStatus}. Expected Registered.");
        }

        var provider = platformType != null
            ? await _providerFactory.GetPlatformInstanceProviderAsync(platformType, cancellationToken)
            : _providerFactory.DefaultProvider;

        if (!provider.IsAvailable)
        {
            throw new InvalidOperationException($"Platform provider '{provider.PlatformType}' is not available.");
        }

        instance = await AddStateTransitionAsync(instance, Status.Creating,
            new Dictionary<string, string> { ["PlatformType"] = provider.PlatformType },
            cancellationToken);

        PlacementProviderRuntimeInfo runtimeInfo;
        try
        {
            runtimeInfo = await provider.DeployInstanceAsync(
                instance.InstanceId,
                instance.Name,
                instance.Configuration!,
                cancellationToken);
        }
        catch (Exception ex)
        {
            await AddStateTransitionAsync(instance, Status.Error,
                new Dictionary<string, string>
                {
                    ["Reason"] = "Deployment failed",
                    ["Error"] = ex.Message
                },
                cancellationToken);
            throw;
        }

        // Update runtime info with sync timestamp
        runtimeInfo = runtimeInfo with { LastSyncedAt = DateTime.UtcNow };

        instance = instance with
        {
            Placement = runtimeInfo,
            LastUpdatedAt = DateTime.UtcNow
        };

        instance = await AddStateTransitionAsync(instance, Status.Created,
            new Dictionary<string, string> { ["PlatformType"] = provider.PlatformType },
            cancellationToken);

        return instance;
    }

    public async Task<DrasiInstance> StartInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        var instance = await _storageService.GetInstanceAsync(instanceId, cancellationToken);

        if (instance.CurrentStatus != Status.Created && instance.CurrentStatus != Status.Stopped)
        {
            throw new InvalidOperationException(
                $"Instance {instanceId} cannot be started from state {instance.CurrentStatus}. Expected Created or Stopped.");
        }

        if (instance.Placement == null)
        {
            throw new InvalidOperationException($"Instance {instanceId} is not deployed.");
        }

        var provider = await _providerFactory.GetPlatformInstanceProviderAsync(
            instance.Placement.PlatformType,
            cancellationToken);

        instance = await AddStateTransitionAsync(instance, Status.Starting,
            new Dictionary<string, string>(),
            cancellationToken);

        PlacementProviderRuntimeInfo runtimeInfo;
        try
        {
            runtimeInfo = await provider.StartInstanceAsync(instanceId, cancellationToken);
        }
        catch (Exception ex)
        {
            await AddStateTransitionAsync(instance, Status.Error,
                new Dictionary<string, string>
                {
                    ["Reason"] = "Start failed",
                    ["Error"] = ex.Message
                },
                cancellationToken);
            throw;
        }

        instance = instance with
        {
            Placement = runtimeInfo with { LastSyncedAt = DateTime.UtcNow },
            LastUpdatedAt = DateTime.UtcNow
        };

        var status = MapProviderStatusToInstanceStatus(runtimeInfo.Status);
        instance = await AddStateTransitionAsync(instance, status,
            new Dictionary<string, string>(),
            cancellationToken);

        return instance;
    }

    public async Task<DrasiInstance> StopInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        var instance = await _storageService.GetInstanceAsync(instanceId, cancellationToken);

        if (instance.CurrentStatus != Status.Running && instance.CurrentStatus != Status.Starting)
        {
            throw new InvalidOperationException(
                $"Instance {instanceId} cannot be stopped from state {instance.CurrentStatus}. Expected Running or Starting.");
        }

        if (instance.Placement == null)
        {
            throw new InvalidOperationException($"Instance {instanceId} is not deployed.");
        }

        var provider = await _providerFactory.GetPlatformInstanceProviderAsync(
            instance.Placement.PlatformType,
            cancellationToken);

        instance = await AddStateTransitionAsync(instance, Status.Stopping,
            new Dictionary<string, string>(),
            cancellationToken);

        PlacementProviderRuntimeInfo runtimeInfo;
        try
        {
            runtimeInfo = await provider.StopInstanceAsync(instanceId, cancellationToken);
        }
        catch (Exception ex)
        {
            await AddStateTransitionAsync(instance, Status.Error,
                new Dictionary<string, string>
                {
                    ["Reason"] = "Stop failed",
                    ["Error"] = ex.Message
                },
                cancellationToken);
            throw;
        }

        instance = instance with
        {
            Placement = runtimeInfo with { LastSyncedAt = DateTime.UtcNow },
            LastUpdatedAt = DateTime.UtcNow
        };

        var status = MapProviderStatusToInstanceStatus(runtimeInfo.Status);
        instance = await AddStateTransitionAsync(instance, status,
            new Dictionary<string, string>(),
            cancellationToken);

        return instance;
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
            if (instance.CurrentStatus == Status.Running || instance.CurrentStatus == Status.Starting)
            {
                await StopInstanceAsync(instanceId, cancellationToken);
                instance = await _storageService.GetInstanceAsync(instanceId, cancellationToken);
            }

            var provider = await _providerFactory.GetPlatformInstanceProviderAsync(
                instance.Placement!.PlatformType,
                cancellationToken);

            instance = await AddStateTransitionAsync(instance, Status.Deleting,
                new Dictionary<string, string>(),
                cancellationToken);

            try
            {
                await provider.DeleteInstanceAsync(instanceId, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to delete infrastructure for {instanceId}: {ex.Message}");
            }
        }

        instance = await AddStateTransitionAsync(instance, Status.Deleted,
            new Dictionary<string, string>(),
            cancellationToken);
    }

    // ========================================
    // CONFIGURATION MANAGEMENT
    // ========================================

    public async Task<DrasiInstance> UpdateInstanceConfigurationAsync(
        string instanceId,
        DrasiConfiguration configuration,
        Dictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentNullException.ThrowIfNull(configuration);

        var instance = await _storageService.GetInstanceAsync(instanceId, cancellationToken);

        if (instance.Placement != null &&
            instance.CurrentStatus != Status.Created &&
            instance.CurrentStatus != Status.Stopped)
        {
            throw new InvalidOperationException(
                $"Instance {instanceId} must be stopped before updating configuration. Current state: {instance.CurrentStatus}");
        }

        var updatedInstance = instance with
        {
            Configuration = configuration,
            MetaData = metadata ?? instance.MetaData,
            LastUpdatedAt = DateTime.UtcNow
        };

        return await _storageService.UpdateInstanceAsync(updatedInstance, cancellationToken);
    }

    public async Task<DrasiInstance> UpdateInstanceMetadataAsync(
        string instanceId,
        Dictionary<string, object?> metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentNullException.ThrowIfNull(metadata);

        var instance = await _storageService.GetInstanceAsync(instanceId, cancellationToken);

        var updatedInstance = instance with
        {
            MetaData = metadata,
            LastUpdatedAt = DateTime.UtcNow
        };

        return await _storageService.UpdateInstanceAsync(updatedInstance, cancellationToken);
    }

    // ========================================
    // QUERY OPERATIONS
    // ========================================

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
                var provider = await _providerFactory.GetPlatformInstanceProviderAsync(
                    instance.Placement.PlatformType,
                    cancellationToken);

                var runtimeInfo = await provider.GetInstanceInfoAsync(instanceId, cancellationToken);

                instance = instance with
                {
                    Placement = runtimeInfo with { LastSyncedAt = DateTime.UtcNow },
                    LastUpdatedAt = DateTime.UtcNow
                };

                var providerStatus = MapProviderStatusToInstanceStatus(runtimeInfo.Status);
                if (instance.CurrentStatus != providerStatus)
                {
                    instance = await AddStateTransitionAsync(instance, providerStatus,
                        new Dictionary<string, string> { ["Reason"] = "State synchronized from provider" },
                        cancellationToken);
                }
                else
                {
                    instance = await _storageService.UpdateInstanceAsync(instance, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to refresh state for {instanceId}: {ex.Message}");
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
        Status status,
        CancellationToken cancellationToken = default)
    {
        var allInstances = await _storageService.GetAllInstancesAsync(cancellationToken);
        return allInstances.Where(i => i.CurrentStatus == status);
    }

    // ========================================
    // PLATFORM INFORMATION
    // ========================================

    public async Task<IEnumerable<PlatformInfo>> GetAvailablePlatformsAsync(
        CancellationToken cancellationToken = default)
    {
        var providers = await _providerFactory.GetAllPlatformInstanceProvidersAsync(cancellationToken);
        var defaultProvider = _providerFactory.DefaultProvider;

        return providers.Select(p => new PlatformInfo
        {
            PlatformType = p.PlatformType,
            IsAvailable = p.IsAvailable,
            IsDefault = p.PlatformType == defaultProvider.PlatformType,
            Metadata = new Dictionary<string, object?>()
        });
    }

    public async Task<PlatformInfo> GetDefaultPlatformAsync(
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        var defaultProvider = _providerFactory.DefaultProvider;

        return new PlatformInfo
        {
            PlatformType = defaultProvider.PlatformType,
            IsAvailable = defaultProvider.IsAvailable,
            IsDefault = true,
            Metadata = new Dictionary<string, object?>()
        };
    }

    // ========================================
    // HELPER METHODS
    // ========================================

    private static Status MapProviderStatusToInstanceStatus(PlacementProviderRuntimeStatus providerStatus)
    {
        return providerStatus switch
        {
            PlacementProviderRuntimeStatus.Unknown => Status.Unknown,
            PlacementProviderRuntimeStatus.Deploying => Status.Creating,
            PlacementProviderRuntimeStatus.Deployed => Status.Created,
            PlacementProviderRuntimeStatus.Starting => Status.Starting,
            PlacementProviderRuntimeStatus.Running => Status.Running,
            PlacementProviderRuntimeStatus.Stopping => Status.Stopping,
            PlacementProviderRuntimeStatus.Stopped => Status.Stopped,
            PlacementProviderRuntimeStatus.Failed => Status.Error,
            PlacementProviderRuntimeStatus.Deleted => Status.Deleted,
            _ => Status.Unknown
        };
    }

    private async Task<DrasiInstance> AddStateTransitionAsync(
        DrasiInstance instance,
        Status newStatus,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        var newState = new DrasiInstanceState
        {
            Status = newStatus,
            TimeStamp = DateTime.UtcNow,
            StateMetadata = metadata
        };

        // Create new stack preserving order: reverse existing stack to get oldest->newest,
        // then create new stack from that (which reverses it back to newest->oldest),
        // then push the new state on top
        var stateHistory = new Stack<DrasiInstanceState>(instance.StateHistory.Reverse());
        stateHistory.Push(newState);

        var updatedInstance = instance with
        {
            StateHistory = stateHistory,
            LastUpdatedAt = DateTime.UtcNow
        };

        return await _storageService.UpdateInstanceAsync(updatedInstance, cancellationToken);
    }
}
