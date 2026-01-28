using System.Collections.Concurrent;
using System.Diagnostics;
using DRaaS.Core.Models;
using DRaaS.Core.Services.Storage;

namespace DRaaS.Core.Services.Monitoring;

/// <summary>
/// Monitors local process instances by polling their status at regular intervals.
/// Publishes status changes to the centralized status update service.
/// </summary>
public class ProcessStatusMonitor : IStatusMonitor
{
    private readonly IInstanceRuntimeStore _runtimeStore;
    private readonly IStatusUpdateService _statusUpdateService;
    private readonly ConcurrentDictionary<string, Process> _processes;
    private readonly TimeSpan _pollingInterval;
    private Task? _monitoringTask;
    private CancellationTokenSource? _cancellationTokenSource;

    public ProcessStatusMonitor(
        IInstanceRuntimeStore runtimeStore,
        IStatusUpdateService statusUpdateService,
        ConcurrentDictionary<string, Process> processes,
        TimeSpan? pollingInterval = null)
    {
        _runtimeStore = runtimeStore;
        _statusUpdateService = statusUpdateService;
        _processes = processes;
        _pollingInterval = pollingInterval ?? TimeSpan.FromSeconds(5); // Default: check every 5 seconds
    }

    public string PlatformType => "Process";

    public bool RequiresPolling => true;

    public Task StartMonitoringAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _monitoringTask = Task.Run(() => MonitorProcessesAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
        return Task.CompletedTask;
    }

    public async Task StopMonitoringAsync()
    {
        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
            if (_monitoringTask != null)
            {
                try
                {
                    await _monitoringTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelling
                }
            }
        }
    }

    private async Task MonitorProcessesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Get all process instances from runtime store
                var processInstances = await _runtimeStore.GetByPlatformAsync(Models.PlatformType.Process);

                foreach (var instance in processInstances)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    await CheckProcessStatusAsync(instance);
                }

                // Wait for next polling interval
                await Task.Delay(_pollingInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                // TODO: Log error
                // Continue monitoring even if one iteration fails
                await Task.Delay(_pollingInterval, cancellationToken);
            }
        }
    }

    private async Task CheckProcessStatusAsync(InstanceRuntimeInfo instance)
    {
        // Skip if already stopped
        if (instance.Status == InstanceStatus.Stopped)
            return;

        // Try to find the process in our tracked dictionary
        if (_processes.TryGetValue(instance.InstanceId, out var process))
        {
            try
            {
                // Check if process has exited
                if (process.HasExited)
                {
                    // Process stopped unexpectedly
                    await _statusUpdateService.PublishStatusUpdateAsync(
                        instance.InstanceId,
                        InstanceStatus.Stopped,
                        "ProcessStatusMonitor",
                        new Dictionary<string, string>
                        {
                            ["ExitCode"] = process.ExitCode.ToString(),
                            ["ExitTime"] = process.ExitTime.ToString("O"),
                            ["Reason"] = "ProcessExited"
                        });

                    // Remove from tracking
                    _processes.TryRemove(instance.InstanceId, out _);
                }
                // else: process is still running, no update needed
            }
            catch (InvalidOperationException)
            {
                // Process object might not have been started or has been disposed
                // Mark as stopped
                await _statusUpdateService.PublishStatusUpdateAsync(
                    instance.InstanceId,
                    InstanceStatus.Error,
                    "ProcessStatusMonitor",
                    new Dictionary<string, string>
                    {
                        ["Reason"] = "ProcessObjectInvalid"
                    });
            }
        }
        else
        {
            // Process not in our dictionary but marked as running in store
            // This could happen after app restart - mark as unknown/error
            await _statusUpdateService.PublishStatusUpdateAsync(
                instance.InstanceId,
                InstanceStatus.Error,
                "ProcessStatusMonitor",
                new Dictionary<string, string>
                {
                    ["Reason"] = "ProcessNotTracked"
                });
        }
    }
}
