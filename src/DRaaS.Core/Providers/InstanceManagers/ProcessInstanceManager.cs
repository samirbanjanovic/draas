using System.Diagnostics;
using System.Text;
using DRaaS.Core.Models;
using DRaaS.Core.Services.Storage;
using DRaaS.Core.Services.ResourceAllocation;
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace DRaaS.Core.Providers.InstanceManagers;

public class ProcessInstanceManager : IDrasiServerInstanceManager
{
    private readonly IInstanceRuntimeStore _runtimeStore;
    private readonly ProcessInstanceManagerOptions _options;
    private readonly ConcurrentDictionary<string, Process> _processes = new();

    public ProcessInstanceManager(
        IInstanceRuntimeStore runtimeStore,
        IOptions<ProcessInstanceManagerOptions> options)
    {
        _runtimeStore = runtimeStore;
        _options = options.Value;

        // Ensure directories exist
        Directory.CreateDirectory(_options.InstanceConfigDirectory);
        Directory.CreateDirectory(_options.WorkingDirectory);
    }

    public string PlatformType => "Process";

    /// <summary>
    /// Provides access to the process dictionary for the status monitor.
    /// </summary>
    public ConcurrentDictionary<string, Process> TrackedProcesses => _processes;

    public async Task<InstanceRuntimeInfo> StartInstanceAsync(string instanceId, Configuration configuration)
    {
        // Create instance-specific configuration file from the store configuration
        var configFilePath = await CreateInstanceConfigurationFileAsync(instanceId, configuration);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = _options.ExecutablePath,
            Arguments = $"--config {configFilePath}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = _options.WorkingDirectory
        };

        var process = new Process
        {
            StartInfo = processStartInfo
        };

        process.Start();
        var processId = process.Id.ToString();

        var runtimeInfo = new InstanceRuntimeInfo
        {
            InstanceId = instanceId,
            Status = InstanceStatus.Running,
            StartedAt = DateTime.UtcNow,
            ProcessId = processId,
            RuntimeMetadata = new Dictionary<string, string>
            {
                ["PlatformType"] = PlatformType,
                ["ExecutablePath"] = processStartInfo.FileName,
                ["Arguments"] = processStartInfo.Arguments,
                ["WorkingDirectory"] = processStartInfo.WorkingDirectory,
                ["ConfigFilePath"] = configFilePath
            }
        };

        await _runtimeStore.SaveAsync(runtimeInfo);
        _processes.TryAdd(instanceId, process);

        return runtimeInfo;
    }

    public async Task<InstanceRuntimeInfo> StopInstanceAsync(string instanceId)
    {
        var runtimeInfo = await _runtimeStore.GetAsync(instanceId);
        if (runtimeInfo == null)
        {
            throw new KeyNotFoundException($"Instance '{instanceId}' not found or not running");
        }

        if (_processes.TryGetValue(instanceId, out var process))
        {
            try
            {
                // Request graceful shutdown
                process.Kill(entireProcessTree: false);

                // Wait for graceful exit with timeout
                var timeoutMs = _options.ShutdownTimeoutSeconds * 1000;
                if (!process.WaitForExit(timeoutMs))
                {
                    // Force kill if timeout exceeded
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(); // Wait for actual termination
                }
            }
            catch (Exception ex)
            {
                // Process might have already exited or been disposed
                // Continue with cleanup
            }
        }

        var stoppedInfo = runtimeInfo with
        {
            Status = InstanceStatus.Stopped,
            StoppedAt = DateTime.UtcNow
        };

        await _runtimeStore.SaveAsync(stoppedInfo);
        _processes.TryRemove(instanceId, out _);

        // Clean up configuration file
        if (runtimeInfo.RuntimeMetadata.TryGetValue("ConfigFilePath", out var configFilePath))
        {
            try
            {
                File.Delete(configFilePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        return stoppedInfo;
    }

    public async Task<InstanceRuntimeInfo> RestartInstanceAsync(string instanceId)
    {
        // Stop and start the process
        await StopInstanceAsync(instanceId);

        // Need configuration to restart
        throw new NotImplementedException("RestartInstanceAsync requires configuration retrieval");
    }

    public async Task<InstanceRuntimeInfo> GetInstanceStatusAsync(string instanceId)
    {
        var runtimeInfo = await _runtimeStore.GetAsync(instanceId);
        if (runtimeInfo == null)
        {
            throw new KeyNotFoundException($"Instance '{instanceId}' not found");
        }

        if (_processes.TryGetValue(instanceId, out var process))
        {
            try
            {
                if (process.HasExited)
                {
                    runtimeInfo = runtimeInfo with
                    {
                        Status = InstanceStatus.Stopped,
                        StoppedAt = DateTime.UtcNow
                    };
                    await _runtimeStore.SaveAsync(runtimeInfo);
                    _processes.TryRemove(instanceId, out _);
                }
            }
            catch (Exception)
            {
                // Process might have been disposed
            }
        }

        return runtimeInfo;
    }

    public async Task<IEnumerable<InstanceRuntimeInfo>> GetAllInstanceStatusesAsync()
    {
        // Return all tracked processes
        return await _runtimeStore.GetByPlatformAsync(Models.PlatformType.Process);
    }

    public Task<bool> IsAvailableAsync()
    {
        // Check if drasi-server executable exists and is accessible
        try
        {
            var executablePath = _options.ExecutablePath;

            // If it's a relative path or just a filename, check if it's in PATH
            if (!Path.IsPathRooted(executablePath))
            {
                // Try to find in PATH or assume it's available
                return Task.FromResult(true);
            }

            // If it's an absolute path, check if file exists
            return Task.FromResult(File.Exists(executablePath));
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<ServerConfiguration> AllocateResourcesAsync(IPortAllocator portAllocator)
    {
        // Process manager runs on localhost and requests a port from the shared allocator
        var port = portAllocator.AllocatePort();

        var config = new ServerConfiguration
        {
            Host = "127.0.0.1", // Processes bind to localhost
            Port = port,
            LogLevel = "info"
        };

        return Task.FromResult(config);
    }

    /// <summary>
    /// Creates a drasi-server YAML configuration file for the instance.
    /// </summary>
    private async Task<string> CreateInstanceConfigurationFileAsync(string instanceId, Configuration configuration)
    {
        var configFilePath = Path.Combine(_options.InstanceConfigDirectory, $"{instanceId}-config.yaml");

        // Build drasi-server YAML configuration
        var yamlConfig = new StringBuilder();
        yamlConfig.AppendLine($"id: {instanceId}");
        yamlConfig.AppendLine($"host: {configuration.Host ?? "0.0.0.0"}");
        yamlConfig.AppendLine($"port: {configuration.Port ?? 8080}");
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
                yamlConfig.AppendLine($"    autoStart: {(source.AutoStart ?? true).ToString().ToLowerInvariant()}");
                // Additional source-specific configuration would go here
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
                    // Indent the query string
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

        await File.WriteAllTextAsync(configFilePath, yamlConfig.ToString());
        return configFilePath;
    }
}
