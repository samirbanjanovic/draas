using Docker.DotNet;
using Docker.DotNet.Models;
using DRaaS.CoreLib.Providers.Abstractions;

namespace DRaaS.CoreLib.Clients.Docker;

/// <summary>
/// Docker client implementation using Docker.DotNet SDK.
/// Provides type-safe, async operations without CLI string parsing.
/// </summary>
public class DockerSdkClient : IDraasDockerClient, IDisposable
{
    private readonly IDockerClient _client;

    public DockerSdkClient(string dockerEndpoint = "npipe://./pipe/docker_engine")
    {
        // Default to Windows named pipe, but can be configured for Unix socket or TCP
        _client = new DockerClientConfiguration(new Uri(dockerEndpoint)).CreateClient();
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.System.PingAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> CreateContainerAsync(
        DockerContainerCreateOptions options,
        CancellationToken cancellationToken = default)
    {
        var createParams = new CreateContainerParameters
        {
            Name = options.ContainerName,
            Image = options.ImageName,
            Cmd = options.Command,
            Labels = options.Labels,
            Env = options.EnvironmentVariables.Select(kvp => $"{kvp.Key}={kvp.Value}").ToList(),
            HostConfig = new HostConfig
            {
                Binds = options.VolumeMounts
                    .Select(kvp => $"{kvp.Key}:{kvp.Value}")
                    .ToList(),
                NetworkMode = options.NetworkName,
                PortBindings = options.PortBindings.ToDictionary(
                    kvp => $"{kvp.Key}/tcp",
                    kvp => (IList<PortBinding>)new List<PortBinding>
                    {
                        new() { HostPort = kvp.Value.ToString() }
                    }
                )
            }
        };

        var response = await _client.Containers.CreateContainerAsync(
            createParams,
            cancellationToken
        );

        return response.ID;
    }

    public async Task StartContainerAsync(
        string containerId,
        CancellationToken cancellationToken = default)
    {
        await _client.Containers.StartContainerAsync(
            containerId,
            new ContainerStartParameters(),
            cancellationToken
        );
    }

    public async Task StopContainerAsync(
        string containerId,
        CancellationToken cancellationToken = default)
    {
        await _client.Containers.StopContainerAsync(
            containerId,
            new ContainerStopParameters { WaitBeforeKillSeconds = 30 },
            cancellationToken
        );
    }

    public async Task RemoveContainerAsync(
        string containerId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        await _client.Containers.RemoveContainerAsync(
            containerId,
            new ContainerRemoveParameters { Force = force },
            cancellationToken
        );
    }

    public async Task<DockerContainerState> GetContainerStateAsync(
        string containerId,
        CancellationToken cancellationToken = default)
    {
        var inspection = await _client.Containers.InspectContainerAsync(
            containerId,
            cancellationToken
        );

        return new DockerContainerState
        {
            ContainerId = inspection.ID,
            Status = MapContainerStatus(inspection.State.Status),
            IsRunning = inspection.State.Running,
            StartedAt = DateTime.TryParse(inspection.State.StartedAt, out var started) ? started : null,
            FinishedAt = DateTime.TryParse(inspection.State.FinishedAt, out var finished) ? finished : null,
            ExitCode = (int?)inspection.State.ExitCode
        };
    }

    public async Task<DockerContainerInspection> InspectContainerAsync(
        string containerId,
        CancellationToken cancellationToken = default)
    {
        var inspection = await _client.Containers.InspectContainerAsync(
            containerId,
            cancellationToken
        );

        var metadata = new Dictionary<string, object?>
        {
            ["Platform"] = inspection.Platform,
            ["Created"] = inspection.Created,
            ["Path"] = inspection.Path,
            ["Args"] = inspection.Args,
            ["Image"] = inspection.Image,
            ["ResolvConfPath"] = inspection.ResolvConfPath,
            ["HostnamePath"] = inspection.HostnamePath,
            ["HostsPath"] = inspection.HostsPath,
            ["LogPath"] = inspection.LogPath,
            ["RestartCount"] = inspection.RestartCount,
            ["Driver"] = inspection.Driver,
            ["MountLabel"] = inspection.MountLabel,
            ["ProcessLabel"] = inspection.ProcessLabel
        };

        return new DockerContainerInspection
        {
            ContainerId = inspection.ID,
            ContainerName = inspection.Name.TrimStart('/'),
            Status = MapContainerStatus(inspection.State.Status),
            IPAddress = inspection.NetworkSettings?.Networks?.FirstOrDefault().Value?.IPAddress,
            Metadata = metadata
        };
    }

    private static DockerContainerStatus MapContainerStatus(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "created" => DockerContainerStatus.Created,
            "running" => DockerContainerStatus.Running,
            "paused" => DockerContainerStatus.Paused,
            "restarting" => DockerContainerStatus.Restarting,
            "exited" => DockerContainerStatus.Exited,
            "dead" => DockerContainerStatus.Dead,
            _ => DockerContainerStatus.Unknown
        };
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
