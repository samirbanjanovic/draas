using DRaaS.CoreLib.Models;

namespace DRaaS.CoreLib.Services;

/// <summary>
/// Unified orchestration service that manages complete instance lifecycle.
/// Coordinates domain persistence and infrastructure placement, maintaining
/// a single source of truth for instance state.
/// </summary>
public interface IInstanceOrchestrationService
{
    // ========================================
    // LIFECYCLE OPERATIONS
    // ========================================

    /// <summary>
    /// Registers a new instance (domain entity only, not deployed).
    /// Instance will be in "Registered" state.
    /// </summary>
    /// <param name="name">Instance name</param>
    /// <param name="description">Instance description</param>
    /// <param name="owners">Owner principals</param>
    /// <param name="configuration">Drasi configuration</param>
    /// <param name="metadata">Optional metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Newly registered instance</returns>
    Task<DrasiInstance> RegisterInstanceAsync(
        string name,
        string description,
        string[] owners,
        DrasiConfiguration configuration,
        Dictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deploys instance to infrastructure.
    /// Creates infrastructure resources but doesn't start them.
    /// Instance transitions from "Registered" → "Creating" → "Created".
    /// </summary>
    /// <param name="instanceId">Instance to deploy</param>
    /// <param name="platformType">Target platform ("Process", "Docker", etc.) or null for auto-select</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Instance with placement information</returns>
    Task<DrasiInstance> DeployInstanceAsync(
        string instanceId,
        string? platformType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a deployed instance.
    /// Instance transitions from "Created" or "Stopped" → "Starting" → "Running".
    /// Updates both infrastructure and domain state.
    /// </summary>
    /// <param name="instanceId">Instance to start</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Instance with updated runtime state</returns>
    Task<DrasiInstance> StartInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a running instance.
    /// Instance transitions from "Running" → "Stopping" → "Stopped".
    /// Infrastructure remains deployed.
    /// </summary>
    /// <param name="instanceId">Instance to stop</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Instance with updated state</returns>
    Task<DrasiInstance> StopInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restarts a running instance (stop then start).
    /// </summary>
    Task<DrasiInstance> RestartInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes instance completely (infrastructure + domain).
    /// Instance transitions to "Deleting" → "Deleted".
    /// </summary>
    /// <param name="instanceId">Instance to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default);

    // ========================================
    // CONFIGURATION MANAGEMENT
    // ========================================

    /// <summary>
    /// Updates instance configuration.
    /// If instance is deployed, it must be stopped first.
    /// Configuration changes require redeployment to take effect.
    /// </summary>
    /// <param name="instanceId">Instance to update</param>
    /// <param name="configuration">New configuration</param>
    /// <param name="metadata">Optional metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated instance</returns>
    Task<DrasiInstance> UpdateInstanceConfigurationAsync(
        string instanceId,
        DrasiConfiguration configuration,
        Dictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates instance metadata only (doesn't affect configuration).
    /// </summary>
    Task<DrasiInstance> UpdateInstanceMetadataAsync(
        string instanceId,
        Dictionary<string, object?> metadata,
        CancellationToken cancellationToken = default);

    // ========================================
    // QUERY OPERATIONS
    // ========================================

    /// <summary>
    /// Gets instance with current runtime state.
    /// If deployed, refreshes infrastructure state from provider.
    /// </summary>
    /// <param name="instanceId">Instance ID</param>
    /// <param name="refreshState">Whether to refresh infrastructure state (default: true)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Instance with current state</returns>
    Task<DrasiInstance> GetInstanceAsync(
        string instanceId,
        bool refreshState = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all instances owned by a specific principal.
    /// </summary>
    Task<IEnumerable<DrasiInstance>> GetInstancesByOwnerAsync(
        string ownerPrincipal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all instances in the system.
    /// </summary>
    Task<IEnumerable<DrasiInstance>> GetAllInstancesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets instances by current status.
    /// </summary>
    Task<IEnumerable<DrasiInstance>> GetInstancesByStatusAsync(
        Status status,
        CancellationToken cancellationToken = default);

    // ========================================
    // PLATFORM INFORMATION
    // ========================================

    /// <summary>
    /// Gets all available platform providers.
    /// </summary>
    Task<IEnumerable<PlatformInfo>> GetAvailablePlatformsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the default platform provider based on availability and configuration.
    /// </summary>
    Task<PlatformInfo> GetDefaultPlatformAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Information about an available platform provider.
/// </summary>
public record PlatformInfo
{
    public required string PlatformType { get; init; }
    public required bool IsAvailable { get; init; }
    public required bool IsDefault { get; init; }
    public Dictionary<string, object?> Metadata { get; init; } = new();
}
