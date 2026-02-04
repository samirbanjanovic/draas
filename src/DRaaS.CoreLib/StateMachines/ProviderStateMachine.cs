using System.Collections.Frozen;
using DRaaS.CoreLib.Models;

namespace DRaaS.CoreLib.StateMachines;

public class InvalidStateTransitionException : InvalidOperationException
{
    public PlacementProviderRuntimeStatus FromState { get; }
    public PlacementProviderRuntimeStatus ToState { get; }
    public IReadOnlySet<PlacementProviderRuntimeStatus> ValidTransitions { get; }

    public InvalidStateTransitionException(
        PlacementProviderRuntimeStatus fromState,
        PlacementProviderRuntimeStatus toState,
        IReadOnlySet<PlacementProviderRuntimeStatus> validTransitions)
        : base(BuildMessage(fromState, toState, validTransitions))
    {
        FromState = fromState;
        ToState = toState;
        ValidTransitions = validTransitions;
    }

    private static string BuildMessage(
        PlacementProviderRuntimeStatus fromState,
        PlacementProviderRuntimeStatus toState,
        IReadOnlySet<PlacementProviderRuntimeStatus> validTransitions)
    {
        var validTransitionsText = validTransitions.Any()
            ? string.Join(", ", validTransitions)
            : "none (terminal state)";

        return $"Instance cannot be set to '{toState}' while in '{fromState}' state. " +
               $"Valid direct transitions from '{fromState}' -> {validTransitionsText}";
    }
}

public static class ProviderStateMachine
{
    private static readonly FrozenDictionary<PlacementProviderRuntimeStatus, FrozenSet<PlacementProviderRuntimeStatus>> ValidTransitions =
        new Dictionary<PlacementProviderRuntimeStatus, FrozenSet<PlacementProviderRuntimeStatus>>
        {
            // Unknown: Can transition to any state (recovery/initialization)
            [PlacementProviderRuntimeStatus.Unknown] = new HashSet<PlacementProviderRuntimeStatus>
            {
                PlacementProviderRuntimeStatus.Deploying,
                PlacementProviderRuntimeStatus.Deployed,
                PlacementProviderRuntimeStatus.Starting,
                PlacementProviderRuntimeStatus.Running,
                PlacementProviderRuntimeStatus.Stopping,
                PlacementProviderRuntimeStatus.Stopped,
                PlacementProviderRuntimeStatus.Failed,
                PlacementProviderRuntimeStatus.Deleted
            }.ToFrozenSet(),

            // Deploying: Initial deployment in progress
            [PlacementProviderRuntimeStatus.Deploying] = new HashSet<PlacementProviderRuntimeStatus>
            {
                PlacementProviderRuntimeStatus.Deployed,   // Deployment succeeded
                PlacementProviderRuntimeStatus.Failed,     // Deployment failed
                PlacementProviderRuntimeStatus.Deleted     // Canceled during deployment
            }.ToFrozenSet(),

            // Deployed: Successfully deployed, not started yet
            [PlacementProviderRuntimeStatus.Deployed] = new HashSet<PlacementProviderRuntimeStatus>
            {
                PlacementProviderRuntimeStatus.Starting,   // Begin starting
                PlacementProviderRuntimeStatus.Running,    // Direct start (some providers)
                PlacementProviderRuntimeStatus.Deleted,    // Delete without starting
                PlacementProviderRuntimeStatus.Failed      // Configuration validation failed
            }.ToFrozenSet(),

            // Starting: Start operation in progress
            [PlacementProviderRuntimeStatus.Starting] = new HashSet<PlacementProviderRuntimeStatus>
            {
                PlacementProviderRuntimeStatus.Running,    // Start succeeded
                PlacementProviderRuntimeStatus.Failed,     // Start failed
                PlacementProviderRuntimeStatus.Stopping    // Cancel during start
            }.ToFrozenSet(),

            // Running: Instance is running
            [PlacementProviderRuntimeStatus.Running] = new HashSet<PlacementProviderRuntimeStatus>
            {
                PlacementProviderRuntimeStatus.Stopping,   // Begin graceful stop
                PlacementProviderRuntimeStatus.Stopped,    // Direct stop or crashed
                PlacementProviderRuntimeStatus.Failed,     // Runtime failure
                PlacementProviderRuntimeStatus.Deleted     // Force delete while running
            }.ToFrozenSet(),

            // Stopping: Stop operation in progress
            [PlacementProviderRuntimeStatus.Stopping] = new HashSet<PlacementProviderRuntimeStatus>
            {
                PlacementProviderRuntimeStatus.Stopped,    // Stop succeeded
                PlacementProviderRuntimeStatus.Failed,     // Stop failed (force kill)
                PlacementProviderRuntimeStatus.Deleted     // Delete during stop
            }.ToFrozenSet(),

            // Stopped: Instance is stopped cleanly
            [PlacementProviderRuntimeStatus.Stopped] = new HashSet<PlacementProviderRuntimeStatus>
            {
                PlacementProviderRuntimeStatus.Starting,   // Restart
                PlacementProviderRuntimeStatus.Running,    // Direct restart
                PlacementProviderRuntimeStatus.Deleted     // Cleanup
            }.ToFrozenSet(),

            // Failed: Instance is in a failed state
            [PlacementProviderRuntimeStatus.Failed] = new HashSet<PlacementProviderRuntimeStatus>
            {
                PlacementProviderRuntimeStatus.Starting,   // Retry start
                PlacementProviderRuntimeStatus.Running,    // Recovery succeeded
                PlacementProviderRuntimeStatus.Stopping,   // Cleanup attempt
                PlacementProviderRuntimeStatus.Stopped,    // Cleanup succeeded
                PlacementProviderRuntimeStatus.Deleted     // Give up and delete
            }.ToFrozenSet(),

            // Deleted: Terminal state, no transitions allowed
            [PlacementProviderRuntimeStatus.Deleted] = FrozenSet<PlacementProviderRuntimeStatus>.Empty
        }
        .ToFrozenDictionary();

    public static bool CanReach(
        PlacementProviderRuntimeStatus from, 
        PlacementProviderRuntimeStatus to)
    {
        if (from == to)
            return true;

        var visited = new HashSet<PlacementProviderRuntimeStatus> { from };
        var queue = new Queue<PlacementProviderRuntimeStatus>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (!ValidTransitions.TryGetValue(current, out var nextStates))
                continue;

            foreach (var nextState in nextStates)
            {
                if (nextState == to)
                    return true;

                if (visited.Add(nextState))
                    queue.Enqueue(nextState);
            }
        }

        return false;
    }

    public static void ValidateTransition(
        PlacementProviderRuntimeStatus from,
        PlacementProviderRuntimeStatus to)
    {
        if (CanReach(from, to))
            return;

        var directTransitions = GetValidTransitions(from);
        throw new InvalidStateTransitionException(from, to, directTransitions);
    }

    public static IReadOnlySet<PlacementProviderRuntimeStatus> GetValidTransitions(
        PlacementProviderRuntimeStatus from)
    {
        if (ValidTransitions.TryGetValue(from, out var allowedStates))
            return allowedStates;

        return FrozenSet<PlacementProviderRuntimeStatus>.Empty;
    }

    public static bool CanStart(PlacementProviderRuntimeStatus currentState)
        => CanReach(currentState, PlacementProviderRuntimeStatus.Running);

    public static bool CanStop(PlacementProviderRuntimeStatus currentState)
        => CanReach(currentState, PlacementProviderRuntimeStatus.Stopped);

    public static bool CanDeploy(PlacementProviderRuntimeStatus? currentState)
        => currentState is null or PlacementProviderRuntimeStatus.Unknown;

    public static bool CanDelete(PlacementProviderRuntimeStatus currentState)
        => currentState != PlacementProviderRuntimeStatus.Deleted;
}
