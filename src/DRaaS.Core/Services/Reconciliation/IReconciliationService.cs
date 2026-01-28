using DRaaS.Core.Models;

namespace DRaaS.Core.Services.Reconciliation;

/// <summary>
/// Service responsible for reconciling desired state (configuration) with actual state (running instances).
/// Implements the reconciliation pattern for ensuring instances match their intended configuration.
/// </summary>
public interface IReconciliationService
{
    /// <summary>
    /// Reconciles a single instance by comparing desired state to actual state
    /// and applying changes if drift is detected.
    /// </summary>
    /// <param name="instanceId">The instance identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The drift detection result including whether reconciliation was needed</returns>
    Task<DriftDetectionResult> ReconcileInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconciles all instances managed by the system.
    /// Iterates through all instances and applies reconciliation to each.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of drift detection results for all instances</returns>
    Task<IEnumerable<DriftDetectionResult>> ReconcileAllInstancesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects configuration drift without applying changes.
    /// Useful for reporting and auditing purposes.
    /// </summary>
    /// <param name="instanceId">The instance identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Drift detection result showing differences between desired and actual state</returns>
    Task<DriftDetectionResult> DetectDriftAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually triggers reconciliation for a specific instance using a specific strategy.
    /// Bypasses automatic reconciliation settings.
    /// </summary>
    /// <param name="instanceId">The instance identifier</param>
    /// <param name="strategy">The reconciliation strategy to use</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The drift detection result</returns>
    Task<DriftDetectionResult> ReconcileWithStrategyAsync(
        string instanceId, 
        ReconciliationStrategy strategy, 
        CancellationToken cancellationToken = default);
}
