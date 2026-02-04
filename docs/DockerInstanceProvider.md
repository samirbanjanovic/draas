# Docker Instance Provider

Docker-based implementation of `IPlatformInstanceProvider` using **Docker.DotNet SDK** for type-safe, robust container management.

## Features

✅ **Type-safe** - No string-based CLI commands  
✅ **Async/await** - Proper async support throughout  
✅ **Testable** - Mock `IDockerClient` for unit tests  
✅ **Flexible** - Works with Docker Desktop, Docker Engine, remote Docker hosts  
✅ **Cross-platform** - Windows (named pipe), Linux/macOS (Unix socket), TCP

## Installation

Add Docker.DotNet NuGet package:

```bash
dotnet add package Docker.DotNet
```

## Configuration

### appsettings.json

```json
{
  "DockerInstanceProvider": {
    "ImageName": "draas/server:latest",
    "ConfigMountPath": "/var/draas/configs",
    "ContainerConfigPath": "/app/config",
    "DefaultLogLevel": "Info",
    "NetworkName": "draas-network"
  }
}
```

### Dependency Injection

```csharp
services.Configure<DockerInstanceProviderOptions>(
    configuration.GetSection("DockerInstanceProvider")
);

// Register Docker provider with default endpoint (auto-detects platform)
services.AddDockerInstanceProvider();

// OR specify custom endpoint
services.AddDockerInstanceProvider("tcp://remote-docker-host:2375");
```

### Docker Endpoints

| Platform | Default Endpoint |
|----------|-----------------|
| Windows | `npipe://./pipe/docker_engine` |
| Linux | `unix:///var/run/docker.sock` |
| macOS | `unix:///var/run/docker.sock` |
| Remote | `tcp://host:2375` (insecure) or `https://host:2376` (TLS) |

## Usage Example

```csharp
var provider = serviceProvider.GetRequiredService<DockerInstanceProvider>();

// Check if Docker is available
if (!provider.IsAvailable)
{
    throw new Exception("Docker is not available");
}

// Deploy (create container, don't start)
var deploymentInfo = new InstanceDeploymentInfo
{
    InstanceId = "my-instance",
    Name = "My Draas Instance",
    Configuration = new DrasiConfiguration
    {
        Host = "0.0.0.0",
        Port = 8080,
        LogLevel = "Info",
        Sources = [...],
        Queries = [...],
        Reactions = [...]
    }
};

var runtimeInfo = await provider.DeployInstanceAsync(deploymentInfo);
Console.WriteLine($"Container created: {runtimeInfo.PlatformMetadata["ContainerId"]}");

// Start container
runtimeInfo = await provider.StartInstanceAsync("my-instance");
Console.WriteLine($"Container started at: {runtimeInfo.StartedAt}");
Console.WriteLine($"IP Address: {runtimeInfo.PlatformMetadata["ContainerIP"]}");

// Stop container
runtimeInfo = await provider.StopInstanceAsync("my-instance");

// Delete container and cleanup
await provider.DeleteInstanceAsync("my-instance");
```

## State Machine

The provider follows the standard state machine:

```
Deployed → Running → Stopped → (back to Running or delete)
                  ↓
               Failed
```

Transitions:
- `DeployInstanceAsync`: Creates container → **Deployed** state
- `StartInstanceAsync`: Starts container → **Running** state
- `StopInstanceAsync`: Stops container → **Stopped** state
- `DeleteInstanceAsync`: Removes container → gone

## Container Lifecycle

### Deploy (Create Container)
- Creates instance-specific config directory
- Generates YAML configuration file
- Creates Docker container with volume mount
- Container is **not started** (Deployed state)

### Start
- Starts the container
- Updates state to Running
- Returns metadata with IP, ports, etc.

### Stop
- Gracefully stops container (30s timeout)
- Force kills if timeout exceeded
- Updates state to Stopped

### Delete
- Force-removes container
- Deletes config directory
- Removes from tracking

## Metadata

The provider returns rich metadata:

```csharp
var info = await provider.GetInstanceInfoAsync("my-instance");

// Basic metadata
var containerId = info.PlatformMetadata["ContainerId"];
var containerName = info.PlatformMetadata["ContainerName"];
var imageName = info.PlatformMetadata["ImageName"];

// Runtime metadata (from Docker inspect)
var state = info.PlatformMetadata["ContainerState"];
var ip = info.PlatformMetadata["ContainerIP"];
var created = info.PlatformMetadata["Created"];
var restartCount = info.PlatformMetadata["RestartCount"];
```

## Architecture

### Abstraction Layer

```
DockerInstanceProvider
        ↓
   IDockerClient (interface)
        ↓
  DockerSdkClient (Docker.DotNet)
```

This allows:
- ✅ Unit testing with mocked `IDockerClient`
- ✅ Swapping implementations (CLI vs SDK)
- ✅ Decoupling from Docker specifics

### Key Components

**`IDockerClient`** - Docker operations abstraction  
**`DockerSdkClient`** - Docker.DotNet implementation  
**`DockerInstanceProvider`** - Provider implementation  
**`DockerInstanceProviderOptions`** - Configuration  
**`DockerRuntimeState`** - Internal state tracking

## Testing

### Unit Tests (Mock Docker)

```csharp
[Fact]
public async Task DeployInstance_CreatesContainer_Success()
{
    // Arrange
    var mockDocker = new Mock<IDockerClient>();
    mockDocker
        .Setup(x => x.CreateContainerAsync(It.IsAny<DockerContainerCreateOptions>(), default))
        .ReturnsAsync("container123");

    var provider = new DockerInstanceProvider(
        Options.Create(new DockerInstanceProviderOptions { ... }),
        Mock.Of<IDrasiConfigurationProvider>(),
        mockDocker.Object
    );

    // Act
    var result = await provider.DeployInstanceAsync(deploymentInfo);

    // Assert
    Assert.Equal(PlacementProviderRuntimeStatus.Deployed, result.Status);
    Assert.Equal("container123", result.PlatformMetadata["ContainerId"]);
}
```

### Integration Tests (Real Docker)

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task DockerProvider_FullLifecycle_WorksCorrectly()
{
    var client = new DockerSdkClient();
    var provider = new DockerInstanceProvider(..., client);

    try
    {
        // Deploy
        var deployed = await provider.DeployInstanceAsync(deploymentInfo);
        Assert.Equal(PlacementProviderRuntimeStatus.Deployed, deployed.Status);

        // Start
        var running = await provider.StartInstanceAsync("test-instance");
        Assert.Equal(PlacementProviderRuntimeStatus.Running, running.Status);

        // Stop
        var stopped = await provider.StopInstanceAsync("test-instance");
        Assert.Equal(PlacementProviderRuntimeStatus.Stopped, stopped.Status);
    }
    finally
    {
        await provider.DeleteInstanceAsync("test-instance");
    }
}
```

## Troubleshooting

### Container Creation Fails

**Error**: "No such image: draas/server:latest"  
**Solution**: Pull the image first: `docker pull draas/server:latest`

### Docker Not Available

**Error**: "Cannot connect to Docker daemon"  
**Solution**: 
- Windows: Ensure Docker Desktop is running
- Linux: Ensure Docker daemon is running and user has permissions
- Check socket/pipe permissions

### Permission Denied

**Error**: "Permission denied while trying to connect"  
**Solution**:
- Linux: Add user to `docker` group: `sudo usermod -aG docker $USER`
- Windows: Run as administrator or ensure Docker Desktop is configured correctly

### Volume Mount Issues

**Error**: "Invalid mount config"  
**Solution**: Ensure `ConfigMountPath` exists and has proper permissions

## Performance

Docker.DotNet SDK advantages over CLI:
- ✅ No process spawning overhead
- ✅ Direct API calls to Docker daemon
- ✅ Streamed responses for large outputs
- ✅ Better connection pooling

Benchmarks (approximate):
- CLI: ~100-200ms per operation
- SDK: ~10-50ms per operation

## Security Considerations

1. **TLS for Remote Docker**: Use `https://` endpoint with certificates
2. **Socket Permissions**: Limit access to Docker socket
3. **Network Isolation**: Use custom Docker networks for Drasi instances
4. **Resource Limits**: Set memory/CPU limits in container creation
5. **Image Security**: Use signed images and vulnerability scanning

## Future Enhancements

- [ ] Resource limits (memory, CPU)
- [ ] Health checks
- [ ] Container restart policies
- [ ] Docker Compose support
- [ ] Swarm/Kubernetes orchestration
- [ ] Multi-container instances
