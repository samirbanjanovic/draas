using DRaaS.CoreLib.Models;

namespace DRaaS.CoreLib.Services;

public interface IInstanceOrchestrationService
{
    // LIFECYCLE OPERATIONS

    // ADVANCED: Register without deploying. Most users should use DeployInstanceAsync.
    Task<DrasiInstance> RegisterInstanceAsync(
        string name,
        string description,
        string[] owners,
        DrasiConfiguration configuration,
        Dictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default);

    Task<DrasiInstance> DeployInstanceAsync(
        string name,
        string description,
        string[] owners,
        DrasiConfiguration configuration,
        string? platformType = null,
        Dictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default);

    Task<DrasiInstance> DeployInstanceAsync(
        string instanceId,
        string? platformType = null,
        CancellationToken cancellationToken = default);

    Task<DrasiInstance> StartInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default);

    Task<DrasiInstance> StopInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default);

    Task<DrasiInstance> RestartInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default);

    Task DeleteInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default);

    Task<DrasiInstance> UpdateInstanceConfigurationAsync(
        string instanceId,
        DrasiConfiguration configuration,
        Dictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default);

    Task<DrasiInstance> UpdateInstanceMetadataAsync(
        string instanceId,
        Dictionary<string, object?> metadata,
        CancellationToken cancellationToken = default);

    Task<DrasiInstance> GetInstanceAsync(
        string instanceId,
        bool refreshState = true,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<DrasiInstance>> GetInstancesByOwnerAsync(
        string ownerPrincipal,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<DrasiInstance>> GetAllInstancesAsync(
        CancellationToken cancellationToken = default);

    Task<IEnumerable<DrasiInstance>> GetInstancesByStatusAsync(
        DomainStatus status,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<PlatformDetails>> GetAvailablePlatformsAsync(
        CancellationToken cancellationToken = default);

    Task<PlatformDetails> GetDefaultPlatformAsync(
        CancellationToken cancellationToken = default);
}

public record PlatformDetails
{
    public required string PlatformType { get; init; }
    public required bool IsAvailable { get; init; }
    public required bool IsDefault { get; init; }

    public int? InstanceCount { get; init; }

    public Dictionary<string, string> Labels { get; init; } = new();
    public Dictionary<string, object?> Metadata { get; init; } = new();
}
