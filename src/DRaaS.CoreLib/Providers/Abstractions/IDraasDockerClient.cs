namespace DRaaS.CoreLib.Providers.Abstractions;

public interface IDraasDockerClient
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    Task<string> CreateContainerAsync(
        DockerContainerCreateOptions options,
        CancellationToken cancellationToken = default);

    Task StartContainerAsync(
        string containerId,
        CancellationToken cancellationToken = default);

    Task StopContainerAsync(
        string containerId,
        CancellationToken cancellationToken = default);

    Task RemoveContainerAsync(
        string containerId,
        bool force = false,
        CancellationToken cancellationToken = default);

    Task<DockerContainerState> GetContainerStateAsync(
        string containerId,
        CancellationToken cancellationToken = default);

    Task<DockerContainerInspection> InspectContainerAsync(
        string containerId,
        CancellationToken cancellationToken = default);
}

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

public record DockerContainerState
{
    public required string ContainerId { get; init; }
    public required DockerContainerStatus Status { get; init; }
    public bool IsRunning { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
    public int? ExitCode { get; init; }
}

public record DockerContainerInspection
{
    public required string ContainerId { get; init; }
    public required string ContainerName { get; init; }
    public required DockerContainerStatus Status { get; init; }
    public string? IPAddress { get; init; }
    public Dictionary<string, object?> Metadata { get; init; } = new();
}

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
