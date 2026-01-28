namespace DRaaS.Core.Services.Reconciliation;

/// <summary>
/// Defines strategies for reconciling configuration drift between desired and actual state.
/// Each strategy represents a different approach to applying configuration changes to running instances.
/// </summary>
public enum ReconciliationStrategy
{
    /// <summary>
    /// Simple restart strategy: Stop the instance, update configuration, start the instance.
    /// Fastest but causes downtime. Suitable for development and non-critical instances.
    /// </summary>
    Restart = 0,

    /// <summary>
    /// Rolling update strategy: Gradually update instances one at a time.
    /// Maintains availability by ensuring some instances remain running during updates.
    /// Best for stateless instances with load balancing.
    /// </summary>
    RollingUpdate = 1,

    /// <summary>
    /// Blue-Green deployment strategy: Create new instance with updated config,
    /// switch traffic when ready, then remove old instance.
    /// Zero downtime but requires double resources temporarily.
    /// Best for critical production instances.
    /// </summary>
    BlueGreen = 2,

    /// <summary>
    /// Canary deployment strategy: Route small percentage of traffic to new configuration,
    /// gradually increase if metrics are healthy, rollback if issues detected.
    /// Safest for production changes with unknown impact.
    /// Requires traffic routing and metrics collection.
    /// </summary>
    Canary = 3,

    /// <summary>
    /// Manual reconciliation: Detect and report drift but don't apply changes automatically.
    /// Requires explicit approval before reconciliation proceeds.
    /// Best for compliance-sensitive environments.
    /// </summary>
    Manual = 4
}
