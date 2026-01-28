namespace DRaaS.Core.Services.Monitoring;

/// <summary>
/// Platform-specific status monitoring strategy.
/// Local platforms (Process) implement polling logic.
/// Distributed platforms (Docker/AKS) rely on external daemons pushing to IStatusUpdateService.
/// </summary>
public interface IStatusMonitor
{
    /// <summary>
    /// Gets the platform type this monitor supports.
    /// </summary>
    string PlatformType { get; }

    /// <summary>
    /// Starts monitoring instances for the platform.
    /// For polling-based monitors, this starts a background task.
    /// For push-based monitors, this is typically a no-op.
    /// </summary>
    Task StartMonitoringAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops monitoring.
    /// </summary>
    Task StopMonitoringAsync();

    /// <summary>
    /// Indicates whether this monitor requires active polling (true for Process)
    /// or relies on external push updates (false for Docker/AKS).
    /// </summary>
    bool RequiresPolling { get; }
}
