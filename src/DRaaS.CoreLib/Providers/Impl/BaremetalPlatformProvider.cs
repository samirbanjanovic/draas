using DRaaS.CoreLib.Models;
using DRaaS.CoreLib.StateMachines;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DRaaS.CoreLib.Providers.Impl;

/// <summary>
/// Configuration options for the process-based instance provider.
/// </summary>
public record BaremetalProviderOptions
{
    /// <summary>
    /// Path to the executable to run for each instance.
    /// Can be absolute or relative (assumes PATH).
    /// </summary>
    public required string ExecutablePath { get; init; }

    /// <summary>
    /// Root directory where instance-specific subdirectories are created.
    /// Each instance gets its own subdirectory: {InstanceConfigDirectory}/{instanceId}/
    /// </summary>
    public required string InstanceConfigDirectory { get; init; }

    /// <summary>
    /// Working directory for the process execution.
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Default log level if not specified in configuration.
    /// </summary>
    public string DefaultLogLevel { get; init; } = "Info";

    /// <summary>
    /// Timeout in seconds to wait for graceful shutdown before force-killing the process.
    /// </summary>
    public int GracefulShutdownTimeoutInSeconds { get; init; } = 30;
}

public class BaremetalPlatformProvider : IPlatformProvider
{
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

    private readonly BaremetalProviderOptions _options;
    private readonly IDrasiConfigurationProvider _configurationProvider;
    private readonly ConcurrentDictionary<string, ProcessRuntimeState> _instances = new();

    public BaremetalPlatformProvider(
        IOptions<BaremetalProviderOptions> options,
        IDrasiConfigurationProvider configurationProvider)
    {
        _options = options.Value;
        _configurationProvider = configurationProvider;
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

    public PlatformInfo GetPlatformInfo()
    {
        var runningCount = _instances.Values.Count(s => 
            s.Status == PlacementProviderRuntimeStatus.Running && 
            (s.Process == null || !s.Process.HasExited));

        return new PlatformInfo
        {
            PlatformType = PlatformType,
            IsAvailable = IsAvailable,
            InstanceCount = runningCount,
            Labels = new Dictionary<string, string>
            {
                ["provider-type"] = "process",
                ["executable"] = Path.GetFileName(_options.ExecutablePath)
            },
            Metadata = new Dictionary<string, object?>
            {
                ["ExecutablePath"] = _options.ExecutablePath,
                ["WorkingDirectory"] = _options.WorkingDirectory,
                ["ConfigDirectory"] = _options.InstanceConfigDirectory,
                ["TotalInstances"] = _instances.Count,
                ["RunningInstances"] = runningCount
            }
        };
    }

    public async Task<PlacementProviderRuntimeInfo> DeployInstanceAsync(
        string instanceId,
        string instanceName,
        DrasiConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        // Check if instance already exists
        if (_instances.TryGetValue(instanceId, out var existingState))
        {
            // If process exists and is running, reject deployment
            if (existingState.Process != null && !existingState.Process.HasExited)
            {
                throw new InvalidOperationException(
                    $"Instance '{instanceId}' is already deployed and active. " +
                    $"Stop or delete the instance before redeploying."
                );
            }

            // If process exists but has exited, or deployment exists, reject
            throw new InvalidOperationException(
                $"Instance '{instanceId}' is already deployed. " +
                $"Delete the instance before redeploying."
            );
        }

        // Create instance-specific directory and configuration file
        var instanceDirectory = Path.Combine(
            _options.InstanceConfigDirectory,
            instanceId
        );

        Directory.CreateDirectory(instanceDirectory);

        var configFilePath = Path.Combine(instanceDirectory, "instance-config.yaml");

        await CreateConfigurationFileAsync(
            instanceId,
            configuration,
            configFilePath,
            cancellationToken
        );

        var deployedAt = DateTime.UtcNow;

        // Track deployment state (not started yet)
        var state = new ProcessRuntimeState
        {
            InstanceId = instanceId,
            Name = instanceName,
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

        ProviderStateMachine.ValidateTransition(
            state.Status,
            PlacementProviderRuntimeStatus.Running
        );

        if (state.Process != null && !state.Process.HasExited)
        {
            throw new InvalidOperationException(
                $"Instance '{instanceId}' has a running process despite status '{state.Status}'. " +
                $"This indicates a state inconsistency. Stop the instance first."
            );
        }

        // Dispose of old process reference if it exists
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
            PlatformMetadata = CreateInstanceMetadata(state.ConfigFilePath, process)
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

        ProviderStateMachine.ValidateTransition(
            state.Status,
            PlacementProviderRuntimeStatus.Stopped
        );

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
                PlatformMetadata = CreateInstanceMetadata(state.ConfigFilePath, state.Process)
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
            PlatformMetadata = CreateInstanceMetadata(state.ConfigFilePath, state.Process)
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

        // Clean up instance directory (includes config and any other instance files)
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
            // Ignore cleanup errors - directory might be in use or already deleted
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

        return Task.FromResult(new PlacementProviderRuntimeInfo
        {
            InstanceId = instanceId,
            PlatformType = PlatformType,
            Status = status,
            DeployedAt = state.DeployedAt,
            StartedAt = state.StartedAt,
            StoppedAt = state.StoppedAt,
            PlatformMetadata = CreateInstanceMetadata(state.ConfigFilePath, state.Process)
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

            return new PlacementProviderRuntimeInfo
            {
                InstanceId = state.InstanceId,
                PlatformType = PlatformType,
                Status = status,
                DeployedAt = state.DeployedAt,
                StartedAt = state.StartedAt,
                StoppedAt = state.StoppedAt,
                PlatformMetadata = CreateInstanceMetadata(state.ConfigFilePath, state.Process)
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

    /// <summary>
    /// Creates comprehensive metadata dictionary from process state and configuration.
    /// Automatically includes all available process information safely.
    /// </summary>
    private Dictionary<string, object?> CreateInstanceMetadata(
        string configFilePath, 
        Process? process)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["ConfigFilePath"] = configFilePath,
            ["ExecutablePath"] = _options.ExecutablePath,
            ["WorkingDirectory"] = _options.WorkingDirectory,
            ["HasExited"] = process?.HasExited ?? true
        };

        if (process != null)
        {
            // Define all process property extractors - automatically handles availability and errors
            var extractors = new (string Key, Func<object?> Extractor)[]
            {
                ("ProcessId", () => process.Id),
                ("ProcessName", () => process.ProcessName),
                ("MachineName", () => process.MachineName),
                ("StartTime", () => !process.HasExited ? process.StartTime : null),
                ("ExitTime", () => process.HasExited ? process.ExitTime : null),
                ("ExitCode", () => process.HasExited ? process.ExitCode : null),
                ("WorkingSet64", () => !process.HasExited ? process.WorkingSet64 : null),
                ("PrivateMemorySize64", () => !process.HasExited ? process.PrivateMemorySize64 : null)
            };

            foreach (var (key, extractor) in extractors)
            {
                SafeExtract(metadata, key, extractor);
            }
        }

        return metadata;
    }

    /// <summary>
    /// Safely extracts a value using the provided extractor and adds it to metadata if successful.
    /// Handles all exceptions silently - missing or unavailable properties are simply not added.
    /// </summary>
    private static void SafeExtract(
        Dictionary<string, object?> metadata, 
        string key, 
        Func<object?> extractor)
    {
        try
        {
            var value = extractor();
            if (value != null)
            {
                metadata[key] = value;
            }
        }
        catch
        {
            // Property not available or threw exception - skip it
        }
    }
}
