using DRaaS.Core.Models;
using DRaaS.CoreLib.Models;
using DRaaS.CoreLib.StateMachines;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace DRaaS.CoreLib.Providers.Impl;

public record ProcessInstanceProviderOptions
{
    public required string ExecutablePath { get; init; }
    public required string InstanceConfigDirectory { get; init; }
    public required string WorkingDirectory { get; init; }
    public string DefaultLogLevel { get; init; } = "Info";
    public int GracefulShutdownTimeoutInSeconds { get; init; } = 30;
}

public class ProcessInstanceProvider : IPlatformInstanceProvider
{
    private readonly ProcessInstanceProviderOptions _options;
    private readonly ConcurrentDictionary<string, ProcessRuntimeState> _instances = new();

    public ProcessInstanceProvider(IOptions<ProcessInstanceProviderOptions> options)
    {
        _options = options.Value;
        EnsureDirectoriesExist();
    }

    public string PlatformType => "Process";

    public bool IsAvailable
    {
        get
        {
            try
            {
                var executablePath = _options.ExecutablePath;

                // If it's a relative path or just a filename, assume it's in PATH
                if (!Path.IsPathRooted(executablePath))
                    return true;

                // If it's an absolute path, check if file exists
                return File.Exists(executablePath);
            }
            catch
            {
                return false;
            }
        }
    }

    public async Task<PlacementProviderRuntimeInfo> DeployInstanceAsync(
        InstanceDeploymentInfo deploymentInfo, 
        CancellationToken cancellationToken = default)
    {
        // Check if instance already exists
        if (_instances.TryGetValue(deploymentInfo.InstanceId, out var existingState))
        {
            // If process exists and is running, reject deployment
            if (existingState.Process != null && !existingState.Process.HasExited)
            {
                throw new InvalidOperationException(
                    $"Instance '{deploymentInfo.InstanceId}' is already deployed and running. " +
                    $"Stop or delete the instance before redeploying."
                );
            }

            // If process exists but has exited, or deployment exists, reject
            throw new InvalidOperationException(
                $"Instance '{deploymentInfo.InstanceId}' is already deployed. " +
                $"Delete the instance before redeploying."
            );
        }

        // Create instance-specific configuration file
        var configFilePath = Path.Combine(
            _options.InstanceConfigDirectory, 
            $"{deploymentInfo.InstanceId}-config.yaml"
        );

        await CreateConfigurationFileAsync(
            deploymentInfo.InstanceId,
            deploymentInfo.Configuration, 
            configFilePath, 
            cancellationToken
        );

        var deployedAt = DateTime.UtcNow;

        // Track deployment state (not started yet)
        var state = new ProcessRuntimeState
        {
            InstanceId = deploymentInfo.InstanceId,
            Name = deploymentInfo.Name,
            ConfigFilePath = configFilePath,
            DeployedAt = deployedAt,
            Status = PlacementProviderRuntimeStatus.Deployed
        };

        _instances.TryAdd(deploymentInfo.InstanceId, state);

        return new PlacementProviderRuntimeInfo
        {
            InstanceId = deploymentInfo.InstanceId,
            PlatformType = PlatformType,
            Status = PlacementProviderRuntimeStatus.Deployed,
            DeployedAt = deployedAt,
            PlatformMetadata = new()
            {
                ["ConfigFilePath"] = configFilePath,
                ["ExecutablePath"] = _options.ExecutablePath,
                ["WorkingDirectory"] = _options.WorkingDirectory
            }
        };
    }

    public Task<PlacementProviderRuntimeInfo> StartInstanceAsync(
        string instanceId, 
        CancellationToken cancellationToken = default)
    {
        if (!_instances.TryGetValue(instanceId, out var state))
        {
            throw new InvalidOperationException(
                $"Instance '{instanceId}' not deployed. Call DeployInstanceAsync first."
            );
        }

        // Validate state transition is allowed
        try
        {
            ProviderStateMachine.ValidateTransition(
                state.Status, 
                PlacementProviderRuntimeStatus.Running
            );
        }
        catch (InvalidStateTransitionException ex)
        {
            throw new InvalidOperationException(
                $"Cannot start instance '{instanceId}': {ex.Message}",
                ex
            );
        }

        // Additional check: Ensure process isn't actually running
        if (state.Process != null && !state.Process.HasExited)
        {
            // State says stopped/failed, but process is running - this is a bug
            throw new InvalidOperationException(
                $"Instance '{instanceId}' has a running process despite status '{state.Status}'. " +
                $"This indicates a state inconsistency. Stop the instance first."
            );
        }

        // Dispose of old process reference if it exists (prevents memory leak)
        if (state.Process != null)
        {
            try
            {
                state.Process.Dispose();
            }
            catch
            {
                // Ignore disposal errors - process might already be disposed
            }
        }

        // Create and start process
        var processStartInfo = new ProcessStartInfo
        {
            FileName = _options.ExecutablePath,
            Arguments = $"--config \"{state.ConfigFilePath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = _options.WorkingDirectory
        };

        var process = new Process { StartInfo = processStartInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            // Update state to Failed if start fails
            var failedState = state with
            {
                Status = PlacementProviderRuntimeStatus.Failed,
                Process = null
            };
            _instances[instanceId] = failedState;

            throw new InvalidOperationException(
                $"Failed to start process for instance '{instanceId}': {ex.Message}", 
                ex
            );
        }

        var startedAt = DateTime.UtcNow;

        // Update state
        var updatedState = state with 
        { 
            Process = process, 
            StartedAt = startedAt,
            Status = PlacementProviderRuntimeStatus.Running
        };
        _instances[instanceId] = updatedState;

        return Task.FromResult(new PlacementProviderRuntimeInfo
        {
            InstanceId = instanceId,
            PlatformType = PlatformType,
            Status = PlacementProviderRuntimeStatus.Running,
            DeployedAt = state.DeployedAt,
            StartedAt = startedAt,
            PlatformMetadata = new()
            {
                ["ProcessId"] = process.Id,
                ["ConfigFilePath"] = state.ConfigFilePath,
                ["ExecutablePath"] = _options.ExecutablePath,
                ["WorkingDirectory"] = _options.WorkingDirectory,
                ["HasExited"] = false
            }
        });
    }

    public async Task<PlacementProviderRuntimeInfo> StopInstanceAsync(
        string instanceId, 
        CancellationToken cancellationToken = default)
    {
        if (!_instances.TryGetValue(instanceId, out var state))
        {
            throw new KeyNotFoundException($"Instance '{instanceId}' not found.");
        }

        // Validate state transition is allowed
        try
        {
            ProviderStateMachine.ValidateTransition(
                state.Status,
                PlacementProviderRuntimeStatus.Stopped
            );
        }
        catch (InvalidStateTransitionException ex)
        {
            throw new InvalidOperationException(
                $"Cannot stop instance '{instanceId}': {ex.Message}",
                ex
            );
        }

        if (state.Process == null)
        {
            throw new InvalidOperationException(
                $"Instance '{instanceId}' has no process reference. State is '{state.Status}'."
            );
        }

        if (state.Process.HasExited)
        {
            // Process already exited - just update state
            var exitedState = state with 
            { 
                StoppedAt = DateTime.UtcNow,
                Status = PlacementProviderRuntimeStatus.Stopped
            };
            _instances[instanceId] = exitedState;

            return new PlacementProviderRuntimeInfo
            {
                InstanceId = instanceId,
                PlatformType = PlatformType,
                Status = PlacementProviderRuntimeStatus.Stopped,
                DeployedAt = state.DeployedAt,
                StartedAt = state.StartedAt,
                StoppedAt = exitedState.StoppedAt,
                PlatformMetadata = new()
                {
                    ["ProcessId"] = state.Process.Id,
                    ["ConfigFilePath"] = state.ConfigFilePath,
                    ["ExitCode"] = state.Process.ExitCode,
                    ["HasExited"] = true
                }
            };
        }

        // Update state to Stopping before attempting to kill
        var stoppingState = state with { Status = PlacementProviderRuntimeStatus.Stopping };
        _instances[instanceId] = stoppingState;

        try
        {
            // Request graceful shutdown
            state.Process.Kill(entireProcessTree: false);

            // Wait for graceful exit with timeout
            var timeoutMs = _options.GracefulShutdownTimeoutInSeconds * 1000;
            var exited = await WaitForExitAsync(state.Process, timeoutMs, cancellationToken);

            if (!exited)
            {
                // Force kill if timeout exceeded
                state.Process.Kill(entireProcessTree: true);
                await WaitForExitAsync(state.Process, 5000, cancellationToken);
            }
        }
        catch (InvalidOperationException)
        {
            // Process already exited during our kill attempt
        }
        catch (Exception ex)
        {
            // Failed to stop - update state to Failed
            var failedState = state with 
            { 
                Status = PlacementProviderRuntimeStatus.Failed
            };
            _instances[instanceId] = failedState;

            throw new InvalidOperationException(
                $"Failed to stop instance '{instanceId}': {ex.Message}", 
                ex
            );
        }

        var stoppedAt = DateTime.UtcNow;

        // Update state to Stopped
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
            PlatformMetadata = new()
            {
                ["ProcessId"] = state.Process?.Id,
                ["ConfigFilePath"] = state.ConfigFilePath,
                ["ExitCode"] = state.Process?.ExitCode,
                ["HasExited"] = true
            }
        };
    }

    public Task DeleteInstanceAsync(
        string instanceId, 
        CancellationToken cancellationToken = default)
    {
        if (!_instances.TryRemove(instanceId, out var state))
        {
            throw new KeyNotFoundException($"Instance '{instanceId}' not found.");
        }

        // Ensure process is stopped
        if (state.Process != null && !state.Process.HasExited)
        {
            try
            {
                state.Process.Kill(entireProcessTree: true);
                state.Process.WaitForExit(5000);
            }
            catch
            {
                // Ignore errors if process already exited
            }
        }

        state.Process?.Dispose();

        // Clean up configuration file
        try
        {
            if (File.Exists(state.ConfigFilePath))
            {
                File.Delete(state.ConfigFilePath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }

        return Task.CompletedTask;
    }

    public Task<PlacementProviderRuntimeInfo> GetInstanceInfoAsync(
        string instanceId, 
        CancellationToken cancellationToken = default)
    {
        if (!_instances.TryGetValue(instanceId, out var state))
        {
            throw new KeyNotFoundException($"Instance '{instanceId}' not found.");
        }

        // Check if process has exited
        var status = state.Status;
        var hasExited = false;
        int? exitCode = null;

        if (state.Process != null)
        {
            hasExited = state.Process.HasExited;
            if (hasExited)
            {
                status = PlacementProviderRuntimeStatus.Stopped;
                try
                {
                    exitCode = state.Process.ExitCode;
                }
                catch
                {
                    // Process might be disposed
                }
            }
        }

        var metadata = new Dictionary<string, object?>
        {
            ["ConfigFilePath"] = state.ConfigFilePath,
            ["ExecutablePath"] = _options.ExecutablePath,
            ["WorkingDirectory"] = _options.WorkingDirectory,
            ["HasExited"] = hasExited
        };

        if (state.Process != null)
        {
            metadata["ProcessId"] = state.Process.Id;
        }

        if (exitCode.HasValue)
        {
            metadata["ExitCode"] = exitCode.Value;
        }

        return Task.FromResult(new PlacementProviderRuntimeInfo
        {
            InstanceId = instanceId,
            PlatformType = PlatformType,
            Status = status,
            DeployedAt = state.DeployedAt,
            StartedAt = state.StartedAt,
            StoppedAt = state.StoppedAt,
            PlatformMetadata = metadata
        });
    }

    public Task<IEnumerable<PlacementProviderRuntimeInfo>> GetAllInstancesAsync(
        CancellationToken cancellationToken = default)
    {
        var instances = _instances.Values.Select(state =>
        {
            var status = state.Status;
            var hasExited = state.Process?.HasExited ?? false;

            if (hasExited && status == PlacementProviderRuntimeStatus.Running)
            {
                status = PlacementProviderRuntimeStatus.Stopped;
            }

            var metadata = new Dictionary<string, object?>
            {
                ["ConfigFilePath"] = state.ConfigFilePath,
                ["HasExited"] = hasExited
            };

            if (state.Process != null)
            {
                metadata["ProcessId"] = state.Process.Id;
            }

            return new PlacementProviderRuntimeInfo
            {
                InstanceId = state.InstanceId,
                PlatformType = PlatformType,
                Status = status,
                DeployedAt = state.DeployedAt,
                StartedAt = state.StartedAt,
                StoppedAt = state.StoppedAt,
                PlatformMetadata = metadata
            };
        });

        return Task.FromResult(instances);
    }

    private async Task CreateConfigurationFileAsync(
        string instanceId,
        DrasiConfiguration configuration, 
        string configFilePath, 
        CancellationToken cancellationToken)
    {
        var yamlConfig = new StringBuilder();
        yamlConfig.AppendLine($"id: {instanceId}");
        yamlConfig.AppendLine($"host: {configuration.Host}");
        yamlConfig.AppendLine($"port: {configuration.Port}");
        yamlConfig.AppendLine($"logLevel: {configuration.LogLevel ?? _options.DefaultLogLevel}");
        yamlConfig.AppendLine("persistConfig: true");
        yamlConfig.AppendLine("persistIndex: false");
        yamlConfig.AppendLine();

        // Add sources if configured
        if (configuration.Sources?.Count > 0)
        {
            yamlConfig.AppendLine("sources:");
            foreach (var source in configuration.Sources)
            {
                yamlConfig.AppendLine($"  - kind: {source.Kind}");
                yamlConfig.AppendLine($"    id: {source.Id}");
                yamlConfig.AppendLine($"    autoStart: {source.AutoStart.ToString().ToLowerInvariant()}");
            }
            yamlConfig.AppendLine();
        }
        else
        {
            yamlConfig.AppendLine("sources: []");
            yamlConfig.AppendLine();
        }

        // Add queries if configured
        if (configuration.Queries?.Count > 0)
        {
            yamlConfig.AppendLine("queries:");
            foreach (var query in configuration.Queries)
            {
                yamlConfig.AppendLine($"  - id: {query.Id}");
                if (!string.IsNullOrWhiteSpace(query.QueryText))
                {
                    yamlConfig.AppendLine($"    query: |");
                    var queryLines = query.QueryText.Split('\n');
                    foreach (var line in queryLines)
                    {
                        yamlConfig.AppendLine($"      {line}");
                    }
                }
                yamlConfig.AppendLine("    sources:");
                if (query.Sources?.Count > 0)
                {
                    foreach (var source in query.Sources)
                    {
                        yamlConfig.AppendLine($"      - sourceId: {source.SourceId}");
                    }
                }
            }
            yamlConfig.AppendLine();
        }
        else
        {
            yamlConfig.AppendLine("queries: []");
            yamlConfig.AppendLine();
        }

        // Add reactions if configured
        if (configuration.Reactions?.Count > 0)
        {
            yamlConfig.AppendLine("reactions:");
            foreach (var reaction in configuration.Reactions)
            {
                yamlConfig.AppendLine($"  - kind: {reaction.Kind}");
                yamlConfig.AppendLine($"    id: {reaction.Id}");
                if (reaction.Queries?.Count > 0)
                {
                    yamlConfig.Append("    queries: [");
                    yamlConfig.Append(string.Join(", ", reaction.Queries));
                    yamlConfig.AppendLine("]");
                }
                else
                {
                    yamlConfig.AppendLine("    queries: []");
                }
            }
            yamlConfig.AppendLine();
        }
        else
        {
            yamlConfig.AppendLine("reactions: []");
            yamlConfig.AppendLine();
        }

        await File.WriteAllTextAsync(configFilePath, yamlConfig.ToString(), cancellationToken);
    }

    private void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(_options.InstanceConfigDirectory);
        Directory.CreateDirectory(_options.WorkingDirectory);
    }

    private static async Task<bool> WaitForExitAsync(
        Process process, 
        int timeoutMs, 
        CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private record ProcessRuntimeState
    {
        public required string InstanceId { get; init; }
        public required string Name { get; init; }
        public required string ConfigFilePath { get; init; }
        public required DateTime DeployedAt { get; init; }
        public Process? Process { get; init; }
        public DateTime? StartedAt { get; init; }
        public DateTime? StoppedAt { get; init; }
        public PlacementProviderRuntimeStatus Status { get; init; }
    }
}
