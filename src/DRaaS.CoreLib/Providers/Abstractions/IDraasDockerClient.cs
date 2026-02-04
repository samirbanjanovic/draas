namespace DRaaS.CoreLib.Providers.Abstractions;

/// <summary>
/// Abstraction for Docker operations. Decouples provider from specific Docker implementation.
/// Can be implemented using Docker CLI, Docker.DotNet SDK, or other Docker clients.
/// </summary>
public interface IDraasDockerClient
{
    /// <summary>
    /// Checks if Docker is available and responsive.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a container but does not start it.
    /// </summary>
    /// <returns>Container ID</returns>
    Task<string> CreateContainerAsync(
        DockerContainerCreateOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts an existing container.
    /// </summary>
    Task StartContainerAsync(
        string containerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a running container.
    /// </summary>
    Task StopContainerAsync(
        string containerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a container (optionally force-remove if running).
    /// </summary>
    Task RemoveContainerAsync(
        string containerId,
        bool force = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current state of a container.
    /// </summary>
    Task<DockerContainerState> GetContainerStateAsync(
        string containerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inspects a container and returns detailed information.
    /// </summary>
    Task<DockerContainerInspection> InspectContainerAsync(
        string containerId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for creating a Docker container.
/// </summary>
public record DockerContainerCreateOptions
{
    public required string ContainerName { get; init; }
    public required string ImageName { get; init; }
    public List<string> Command { get; init; } = [];
    public Dictionary<string, string> VolumeMounts { get; init; } = new();
    public Dictionary<string, string> EnvironmentVariables { get; init; } = new();
    public Dictionary<string, string> Labels { get; init; } = new();
    public string? NetworkName { get; init; }
    public Dictionary<int, int> PortBindings { get; init; } = new();
}

/// <summary>
/// Container state information.
/// </summary>
public record DockerContainerState
{
    public required string ContainerId { get; init; }
    public required DockerContainerStatus Status { get; init; }
    public bool IsRunning { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
    public int? ExitCode { get; init; }
}

/// <summary>
/// Detailed container inspection data.
/// </summary>
public record DockerContainerInspection
{
    public required string ContainerId { get; init; }
    public required string ContainerName { get; init; }
    public required DockerContainerStatus Status { get; init; }
    public string? IPAddress { get; init; }
    public Dictionary<string, object?> Metadata { get; init; } = new();
}

/// <summary>
/// Docker container status enum.
/// </summary>
public enum DockerContainerStatus
{
    Created,
    Running,
    Paused,
    Restarting,
    Exited,
    Dead,
    Unknown
}
