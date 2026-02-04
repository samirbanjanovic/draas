using DRaaS.CoreLib.Models;
using DRaaS.CoreLib.StateMachines;
using DRaaS.CoreLib.Providers.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace DRaaS.CoreLib.Providers.Impl;

/// <summary>
/// Configuration options for the Docker-based instance provider.
/// </summary>
public record DockerInstanceProviderOptions
{
    /// <summary>
    /// Docker image to use for instances.
    /// Example: "draas/server:latest"
    /// </summary>
    public required string ImageName { get; init; }

    /// <summary>
    /// Host directory to mount for instance configurations.
    /// Each instance gets: {ConfigMountPath}/{instanceId}/instance-config.yaml
    /// </summary>
    public required string ConfigMountPath { get; init; }

    /// <summary>
    /// Container path where config is mounted.
    /// </summary>
    public string ContainerConfigPath { get; init; } = "/app/config";

    /// <summary>
    /// Default log level if not specified in configuration.
    /// </summary>
    public string DefaultLogLevel { get; init; } = "Info";

    /// <summary>
    /// Network name for container networking. If null, uses default bridge.
    /// </summary>
    public string? NetworkName { get; init; }
}

public class DockerInstanceProvider : IPlatformInstanceProvider
{
    private record DockerRuntimeState
    {
        public required string InstanceId { get; init; }
        public required string Name { get; init; }
        public required string ContainerName { get; init; }
        public required string ConfigFilePath { get; init; }
        public required DateTime DeployedAt { get; init; }
        public string? ContainerId { get; init; }
        public DateTime? StartedAt { get; init; }
        public DateTime? StoppedAt { get; init; }
        public PlacementProviderRuntimeStatus Status { get; init; }
    }

    private readonly DockerInstanceProviderOptions _options;
    private readonly IDrasiConfigurationProvider _configurationProvider;
    private readonly IDraasDockerClient _dockerClient;
    private readonly ConcurrentDictionary<string, DockerRuntimeState> _instances = new();

    public DockerInstanceProvider(
        IOptions<DockerInstanceProviderOptions> options,
        IDrasiConfigurationProvider configurationProvider,
        IDraasDockerClient dockerClient)
    {
        _options = options.Value;
        _configurationProvider = configurationProvider;
        _dockerClient = dockerClient;
        EnsureDirectoriesExist();
    }

    public string PlatformType => "Docker";

    public bool IsAvailable
    {
        get
        {
            try
            {
                return _dockerClient.IsAvailableAsync().GetAwaiter().GetResult();
            }
            catch
            {
                return false;
            }
        }
    }

    public async Task<PlacementProviderRuntimeInfo> DeployInstanceAsync(
        string instanceId,
        string instanceName,
        DrasiConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (_instances.ContainsKey(instanceId))
        {
            throw new InvalidOperationException(
                $"Instance '{instanceId}' is already deployed. " +
                $"Delete the instance before redeploying."
            );
        }

        var instanceDirectory = Path.Combine(
            _options.ConfigMountPath,
            instanceId
        );

        if (Directory.Exists(instanceDirectory))
        {
            try
            {
                Directory.Delete(instanceDirectory, recursive: true);
            }
            catch (IOException)
            {
                // Directory might be in use - let CreateDirectory handle it
            }
        }

        Directory.CreateDirectory(instanceDirectory);

        var configFilePath = Path.Combine(instanceDirectory, "instance-config.yaml");

        try
        {
            await CreateConfigurationFileAsync(
                instanceId,
                configuration,
                configFilePath,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            // Cleanup directory if config creation fails
            CleanupInstanceDirectory(instanceDirectory);
            throw new InvalidOperationException(
                $"Failed to create configuration for instance '{instanceId}': {ex.Message}",
                ex
            );
        }

        var containerName = $"draas-{instanceId}";

        // Create container but don't start it
        var createOptions = new DockerContainerCreateOptions
        {
            ContainerName = containerName,
            ImageName = _options.ImageName,
            Command = ["--config", $"{_options.ContainerConfigPath}/instance-config.yaml"],
            VolumeMounts = new Dictionary<string, string>
            {
                [instanceDirectory] = _options.ContainerConfigPath
            },
            NetworkName = _options.NetworkName,
            Labels = new Dictionary<string, string>
            {
                ["draas.instance.id"] = instanceId,
                ["draas.instance.name"] = instanceName
            }
        };

        string containerId;
        try
        {
            containerId = await _dockerClient.CreateContainerAsync(createOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            // Cleanup directory if container creation fails
            CleanupInstanceDirectory(instanceDirectory);
            throw new InvalidOperationException(
                $"Failed to create Docker container for instance '{instanceId}': " +
                $"{ex.Message}",
                ex
            );
        }

        var deployedAt = DateTime.UtcNow;

        var state = new DockerRuntimeState
        {
            InstanceId = instanceId,
            Name = instanceName,
            ContainerName = containerName,
            ContainerId = containerId,
            ConfigFilePath = configFilePath,
            DeployedAt = deployedAt,
            Status = PlacementProviderRuntimeStatus.Deployed
        };

        _instances.TryAdd(instanceId, state);

        return new PlacementProviderRuntimeInfo
        {
            InstanceId = instanceId,
            PlatformType = PlatformType,
            Status = PlacementProviderRuntimeStatus.Deployed,
            DeployedAt = deployedAt,
            PlatformMetadata = await CreateInstanceMetadataAsync(state, cancellationToken)
        };
    }

    public async Task<PlacementProviderRuntimeInfo> StartInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        if (!_instances.TryGetValue(instanceId, out var state))
        {
            throw new InvalidOperationException(
                $"Instance '{instanceId}' not deployed. Call DeployInstanceAsync first."
            );
        }

        if(state.ContainerId is null)
        {
            throw new InvalidOperationException(
                $"Instance '{instanceId}' has no associated container ID."
            );
        }

        ProviderStateMachine.ValidateTransition(
            state.Status,
            PlacementProviderRuntimeStatus.Running
        );

        // Start the container
        try
        {
            await _dockerClient.StartContainerAsync(state.ContainerId, cancellationToken);
        }
        catch (Exception ex)
        {
            var failedState = state with
            {
                Status = PlacementProviderRuntimeStatus.Failed
            };
            _instances[instanceId] = failedState;

            throw new InvalidOperationException(
                $"Failed to start Docker container for instance '{instanceId}': {ex.Message}",
                ex
            );
        }

        var startedAt = DateTime.UtcNow;

        var updatedState = state with
        {
            StartedAt = startedAt,
            Status = PlacementProviderRuntimeStatus.Running
        };
        _instances[instanceId] = updatedState;

        return new PlacementProviderRuntimeInfo
        {
            InstanceId = instanceId,
            PlatformType = PlatformType,
            Status = PlacementProviderRuntimeStatus.Running,
            DeployedAt = state.DeployedAt,
            StartedAt = startedAt,
            PlatformMetadata = await CreateInstanceMetadataAsync(updatedState, cancellationToken)
        };
    }

    public async Task<PlacementProviderRuntimeInfo> StopInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        if (!_instances.TryGetValue(instanceId, out var state))
        {
            throw new KeyNotFoundException($"Instance '{instanceId}' not found.");
        }

        ProviderStateMachine.ValidateTransition(
            state.Status,
            PlacementProviderRuntimeStatus.Stopped
        );

        // Check if container is already stopped
        var containerState = await _dockerClient.GetContainerStateAsync(
            state.ContainerId!,
            cancellationToken
        );

        if (!containerState.IsRunning)
        {
            var stoppedState = state with
            {
                StoppedAt = DateTime.UtcNow,
                Status = PlacementProviderRuntimeStatus.Stopped
            };
            _instances[instanceId] = stoppedState;

            return new PlacementProviderRuntimeInfo
            {
                InstanceId = instanceId,
                PlatformType = PlatformType,
                Status = PlacementProviderRuntimeStatus.Stopped,
                DeployedAt = state.DeployedAt,
                StartedAt = state.StartedAt,
                StoppedAt = stoppedState.StoppedAt,
                PlatformMetadata = await CreateInstanceMetadataAsync(stoppedState, cancellationToken)
            };
        }

        var stoppingState = state with { Status = PlacementProviderRuntimeStatus.Stopping };
        _instances[instanceId] = stoppingState;

        // Stop the container
        try
        {
            await _dockerClient.StopContainerAsync(state.ContainerId!, cancellationToken);
        }
        catch (Exception ex)
        {
            var failedState = state with
            {
                Status = PlacementProviderRuntimeStatus.Failed
            };
            _instances[instanceId] = failedState;

            throw new InvalidOperationException(
                $"Failed to stop Docker container for instance '{instanceId}': {ex.Message}",
                ex
            );
        }

        var stoppedAt = DateTime.UtcNow;

        var updatedState = state with
        {
            StoppedAt = stoppedAt,
            Status = PlacementProviderRuntimeStatus.Stopped
        };
        _instances[instanceId] = updatedState;

        return new PlacementProviderRuntimeInfo
        {
            InstanceId = instanceId,
            PlatformType = PlatformType,
            Status = PlacementProviderRuntimeStatus.Stopped,
            DeployedAt = state.DeployedAt,
            StartedAt = state.StartedAt,
            StoppedAt = stoppedAt,
            PlatformMetadata = await CreateInstanceMetadataAsync(updatedState, cancellationToken)
        };
    }

    public async Task DeleteInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        if (!_instances.TryRemove(instanceId, out var state))
        {
            throw new KeyNotFoundException($"Instance '{instanceId}' not found.");
        }

        // Remove the container (force if running)
        try
        {
            await _dockerClient.RemoveContainerAsync(state.ContainerId!, force: true, cancellationToken);
        }
        catch
        {
            // Ignore errors - container might already be removed
        }

        // Clean up instance directory
        try
        {
            var instanceDirectory = Path.GetDirectoryName(state.ConfigFilePath);
            if (!string.IsNullOrEmpty(instanceDirectory) && Directory.Exists(instanceDirectory))
            {
                Directory.Delete(instanceDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    public async Task<PlacementProviderRuntimeInfo> GetInstanceInfoAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        if (!_instances.TryGetValue(instanceId, out var state))
        {
            throw new KeyNotFoundException($"Instance '{instanceId}' not found.");
        }

        // Refresh status from Docker
        var currentStatus = await GetContainerStatusAsync(state.ContainerId, cancellationToken);

        // Update state if status changed
        if (currentStatus != state.Status)
        {
            state = state with { Status = currentStatus };
            _instances[instanceId] = state;
        }

        return new PlacementProviderRuntimeInfo
        {
            InstanceId = instanceId,
            PlatformType = PlatformType,
            Status = state.Status,
            DeployedAt = state.DeployedAt,
            StartedAt = state.StartedAt,
            StoppedAt = state.StoppedAt,
            PlatformMetadata = await CreateInstanceMetadataAsync(state, cancellationToken)
        };
    }

    public async Task<IEnumerable<PlacementProviderRuntimeInfo>> GetAllInstancesAsync(
        CancellationToken cancellationToken = default)
    {
        var instances = new List<PlacementProviderRuntimeInfo>();

        foreach (var state in _instances.Values)
        {
            // Refresh status from Docker
            var currentStatus = await GetContainerStatusAsync(state.ContainerId, cancellationToken);

            instances.Add(new PlacementProviderRuntimeInfo
            {
                InstanceId = state.InstanceId,
                PlatformType = PlatformType,
                Status = currentStatus,
                DeployedAt = state.DeployedAt,
                StartedAt = state.StartedAt,
                StoppedAt = state.StoppedAt,
                PlatformMetadata = await CreateInstanceMetadataAsync(state, cancellationToken)
            });
        }

        return instances;
    }

    private async Task CreateConfigurationFileAsync(
        string instanceId,
        DrasiConfiguration configuration,
        string configFilePath,
        CancellationToken cancellationToken)
    {
        var additionalSettings = new Dictionary<string, object?>
        {
            ["DefaultLogLevel"] = _options.DefaultLogLevel
        };

        var yamlContent = _configurationProvider.GenerateConfiguration(
            instanceId,
            configuration,
            additionalSettings
        );

        await File.WriteAllTextAsync(configFilePath, yamlContent, cancellationToken);
    }

    private void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(_options.ConfigMountPath);
    }

    private static void CleanupInstanceDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup - don't throw on cleanup failures
        }
    }

    private async Task<PlacementProviderRuntimeStatus> GetContainerStatusAsync(
        string? containerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(containerId))
        {
            return PlacementProviderRuntimeStatus.Deployed;
        }

        try
        {
            var state = await _dockerClient.GetContainerStateAsync(containerId, cancellationToken);

            return state.Status switch
            {
                DockerContainerStatus.Running => PlacementProviderRuntimeStatus.Running,
                DockerContainerStatus.Exited => PlacementProviderRuntimeStatus.Stopped,
                DockerContainerStatus.Created => PlacementProviderRuntimeStatus.Deployed,
                DockerContainerStatus.Paused => PlacementProviderRuntimeStatus.Stopped,
                DockerContainerStatus.Restarting => PlacementProviderRuntimeStatus.Running,
                DockerContainerStatus.Dead => PlacementProviderRuntimeStatus.Failed,
                _ => PlacementProviderRuntimeStatus.Failed
            };
        }
        catch
        {
            return PlacementProviderRuntimeStatus.Failed;
        }
    }

    private async Task<Dictionary<string, object?>> CreateInstanceMetadataAsync(
        DockerRuntimeState state,
        CancellationToken cancellationToken = default)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["ContainerId"] = state.ContainerId,
            ["ContainerName"] = state.ContainerName,
            ["ImageName"] = _options.ImageName,
            ["ConfigFilePath"] = state.ConfigFilePath,
            ["ConfigMountPath"] = _options.ConfigMountPath
        };

        // Get detailed container inspection from Docker
        if (!string.IsNullOrEmpty(state.ContainerId))
        {
            try
            {
                var inspection = await _dockerClient.InspectContainerAsync(
                    state.ContainerId,
                    cancellationToken
                );

                metadata["ContainerState"] = inspection.Status.ToString();
                metadata["ContainerIP"] = inspection.IPAddress;

                // Add additional metadata from inspection
                foreach (var (key, value) in inspection.Metadata)
                {
                    metadata[key] = value;
                }
            }
            catch
            {
                // Container inspection failed - metadata will be partial
            }
        }

        return metadata;
    }
}
