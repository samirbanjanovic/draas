using System.Diagnostics;
using DRaaS.Core.Models;
using DRaaS.Core.Services.Storage;
using DRaaS.Core.Services.ResourceAllocation;
using System.Collections.Concurrent;

namespace DRaaS.Core.Providers.InstanceManagers;

public class ProcessInstanceManager : IDrasiServerInstanceManager
{
    private readonly IInstanceRuntimeStore _runtimeStore;
    private readonly ConcurrentDictionary<string, Process> _processes = new();

    public ProcessInstanceManager(IInstanceRuntimeStore runtimeStore)
    {
        _runtimeStore = runtimeStore;
    }

    public string PlatformType => "Process";

    /// <summary>
    /// Provides access to the process dictionary for the status monitor.
    /// </summary>
    public ConcurrentDictionary<string, Process> TrackedProcesses => _processes;

    public async Task<InstanceRuntimeInfo> StartInstanceAsync(string instanceId, Configuration configuration)
    {
        // TODO: Implement bare metal process startup
        // 1. Resolve path to Drasi server executable
        // 2. Write configuration to temp file
        // 3. Build command-line arguments:
        //    --config-file <path>
        //    --host <host>
        //    --port <port>
        //    --log-level <level>
        // 4. Create Process with ProcessStartInfo
        // 5. Start process and capture PID

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "drasi-server", // TODO: Configure executable path
            Arguments = $"--host {configuration.Host ?? "0.0.0.0"} --port {configuration.Port ?? 8080} --log-level {configuration.LogLevel ?? "info"}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = new Process
        {
            StartInfo = processStartInfo
        };

        // process.Start(); // Commented out for stub
        var processId = "12345"; // process.Id.ToString(); // Stub PID

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
                ["WorkingDirectory"] = Environment.CurrentDirectory
            }
        };

        await _runtimeStore.SaveAsync(runtimeInfo);
        _processes.TryAdd(instanceId, process);

        return runtimeInfo;
    }

    public async Task<InstanceRuntimeInfo> StopInstanceAsync(string instanceId)
    {
        // TODO: Implement process termination
        // 1. Get Process from _processes dictionary
        // 2. Send graceful shutdown signal (SIGTERM equivalent)
        // 3. Wait for process exit with timeout
        // 4. Force kill if timeout exceeded (SIGKILL)

        var runtimeInfo = await _runtimeStore.GetAsync(instanceId);
        if (runtimeInfo == null)
        {
            throw new KeyNotFoundException($"Instance '{instanceId}' not found or not running");
        }

        if (_processes.TryGetValue(instanceId, out var process))
        {
            try
            {
                // process.Kill(); // Commented for stub
                // process.WaitForExit(5000); // Wait up to 5 seconds
            }
            catch (Exception ex)
            {
                // Log error
            }
        }

        var stoppedInfo = runtimeInfo with
        {
            Status = InstanceStatus.Stopped,
            StoppedAt = DateTime.UtcNow
        };

        await _runtimeStore.SaveAsync(stoppedInfo);
        _processes.TryRemove(instanceId, out _);

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
        // TODO: Check if process is still running
        // 1. Get Process from dictionary
        // 2. Check process.HasExited
        // 3. Update status accordingly

        var runtimeInfo = await _runtimeStore.GetAsync(instanceId);
        if (runtimeInfo == null)
        {
            throw new KeyNotFoundException($"Instance '{instanceId}' not found");
        }

        if (_processes.TryGetValue(instanceId, out var process))
        {
            try
            {
                // if (process.HasExited) // Commented for stub
                // {
                //     runtimeInfo = runtimeInfo with
                //     {
                //         Status = InstanceStatus.Stopped,
                //         StoppedAt = DateTime.UtcNow
                //     };
                //     await _runtimeStore.SaveAsync(runtimeInfo);
                // }
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
        // TODO: Check if Drasi server executable exists
        // Check if required permissions are available
        return Task.FromResult(true); // Stub: assume available
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
}
