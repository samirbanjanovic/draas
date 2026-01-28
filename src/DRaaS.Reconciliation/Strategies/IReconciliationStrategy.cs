using DRaaS.Core.Models;
using DRaaS.Core.Services.Reconciliation;

namespace DRaaS.Reconciliation.Strategies;

/// <summary>
/// Interface for reconciliation strategy implementations.
/// Each strategy defines how to apply configuration changes to running instances.
/// </summary>
public interface IReconciliationStrategy
{
    /// <summary>
    /// Gets the strategy type this implementation handles.
    /// </summary>
    ReconciliationStrategy StrategyType { get; }

    /// <summary>
    /// Applies configuration changes to an instance using this strategy.
    /// </summary>
    /// <param name="instanceId">The instance identifier</param>
    /// <param name="desiredConfiguration">The configuration to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if reconciliation was successful</returns>
    Task<bool> ApplyAsync(
        string instanceId,
        Configuration desiredConfiguration,
        CancellationToken cancellationToken);
}
