# Docker CLI Interop Anti-Pattern Issue

## Problem
The current `DockerInstanceProvider` implementation directly executes Docker CLI commands using string concatenation and text parsing. This is an anti-pattern for several reasons:

### Issues with Current Approach

#### 1. **Brittle String Concatenation**
```csharp
// Current approach - fragile and error-prone
var createArgs = $"create --name {containerName} -v {path}:{mountPath} {imageName}";
```

Problems:
- No escaping of special characters
- Hard to handle complex arguments (quotes, spaces, etc.)
- Easy to introduce injection vulnerabilities
- Difficult to compose arguments conditionally

#### 2. **Text Parsing of Output**
```csharp
// Parsing unstructured text output
var status = result.Output.Trim().ToLowerInvariant();
return status switch { "running" => Running, "exited" => Stopped, ... };
```

Problems:
- Docker CLI output format can change
- Localization issues (Docker CLI might output in different languages)
- No type safety
- Hard to parse complex JSON output

#### 3. **Poor Testability**
- Can't mock Docker CLI
- Tests require Docker installed
- Hard to simulate error conditions
- Slow tests (spawning processes)

#### 4. **No Type Safety**
- Arguments are strings, easy to make mistakes
- Return values are strings, need manual parsing
- No compile-time validation

#### 5. **Platform-Specific Issues**
- Windows vs Linux path differences
- Shell escaping differences
- Docker Desktop vs Docker Engine differences

---

## Solution: Abstraction Layer

### Create `IDockerClient` Interface

```csharp
public interface IDockerClient
{
    Task<string> CreateContainerAsync(DockerContainerCreateOptions options, ...);
    Task StartContainerAsync(string containerId, ...);
    Task<DockerContainerState> GetContainerStateAsync(string containerId, ...);
    // ... etc
}
```

### Benefits

1. **Decoupling**: Provider doesn't know how Docker is accessed
2. **Testability**: Easy to mock `IDockerClient` in unit tests
3. **Flexibility**: Can swap implementations:
   - `DockerCliClient` - wraps CLI calls (current)
   - `DockerSdkClient` - uses Docker.DotNet library
   - `DockerRemoteClient` - calls Docker API over HTTP
4. **Type Safety**: Strong typing for options and return values
5. **Error Handling**: Structured exceptions instead of parsing stderr

---

## Implementation Strategy

### Phase 1: Create Abstraction (Immediate)
✅ Define `IDockerClient` interface  
✅ Define `DockerContainerCreateOptions`, `DockerContainerState`, etc.  
⏳ Implement `DockerCliClient` that wraps current CLI calls  
⏳ Refactor `DockerInstanceProvider` to use `IDockerClient`

### Phase 2: Improve CLI Client (Short-term)
⏳ Better argument escaping/quoting  
⏳ Parse JSON output from `docker inspect --format json`  
⏳ Use `docker` Go template formatting for structured output  
⏳ Handle stderr properly (structured error messages)

### Phase 3: SDK Alternative (Future)
⏳ Evaluate Docker.DotNet library  
⏳ Implement `DockerSdkClient` using Docker.DotNet  
⏳ Make client configurable (CLI vs SDK)  
⏳ Benchmark performance differences

---

## Detailed Design

### Interface Design

```csharp
// DRaaS.CoreLib/Providers/Abstractions/IDockerClient.cs
public interface IDockerClient
{
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    
    Task<string> CreateContainerAsync(
        DockerContainerCreateOptions options, 
        CancellationToken ct = default);
    
    Task StartContainerAsync(string containerId, CancellationToken ct = default);
    Task StopContainerAsync(string containerId, CancellationToken ct = default);
    Task RemoveContainerAsync(string containerId, bool force, CancellationToken ct = default);
    
    Task<DockerContainerState> GetContainerStateAsync(string containerId, CancellationToken ct = default);
    Task<DockerContainerInspection> InspectContainerAsync(string containerId, CancellationToken ct = default);
}
```

### Options Object (Type-Safe Configuration)

```csharp
public record DockerContainerCreateOptions
{
    public required string ContainerName { get; init; }
    public required string ImageName { get; init; }
    public List<string> Command { get; init; } = [];
    public Dictionary<string, string> VolumeMounts { get; init; } = new();
    public Dictionary<string, string> EnvironmentVariables { get; init; } = new();
    public string? NetworkName { get; init; }
    public Dictionary<int, int> PortBindings { get; init; } = new();
}
```

### Return Types (Structured Data)

```csharp
public record DockerContainerState
{
    public required string ContainerId { get; init; }
    public required DockerContainerStatus Status { get; init; }
    public bool IsRunning { get; init; }
    public DateTime? StartedAt { get; init; }
    public int? ExitCode { get; init; }
}

public enum DockerContainerStatus
{
    Created, Running, Paused, Restarting, Exited, Dead, Unknown
}
```

---

## CLI Client Implementation

### Improved CLI Wrapper

```csharp
// DRaaS.CoreLib/Providers/Impl/Docker/DockerCliClient.cs
public class DockerCliClient : IDockerClient
{
    private readonly string _dockerPath;
    
    public async Task<string> CreateContainerAsync(
        DockerContainerCreateOptions options, 
        CancellationToken ct)
    {
        // Build arguments with proper escaping
        var args = new ArgumentBuilder()
            .Add("create")
            .Add("--name", EscapeArg(options.ContainerName));
            
        foreach (var (host, container) in options.VolumeMounts)
            args.Add("-v", $"{EscapeArg(host)}:{EscapeArg(container)}");
            
        if (options.NetworkName != null)
            args.Add("--network", EscapeArg(options.NetworkName));
            
        args.Add(EscapeArg(options.ImageName));
        
        foreach (var cmd in options.Command)
            args.Add(EscapeArg(cmd));
        
        // Execute with structured error handling
        var result = await ExecuteAsync(args.ToString(), ct);
        
        if (result.ExitCode != 0)
            throw new DockerException($"Failed to create container: {result.Error}");
            
        return result.Output.Trim(); // Container ID
    }
    
    public async Task<DockerContainerState> GetContainerStateAsync(
        string containerId, 
        CancellationToken ct)
    {
        // Use JSON output for structured parsing
        var args = $"inspect --format {{{{json .State}}}} {EscapeArg(containerId)}";
        var result = await ExecuteAsync(args, ct);
        
        if (result.ExitCode != 0)
            throw new DockerException($"Container not found: {containerId}");
        
        // Parse JSON instead of string matching
        var stateJson = JsonSerializer.Deserialize<JsonElement>(result.Output);
        
        return new DockerContainerState
        {
            ContainerId = containerId,
            Status = ParseStatus(stateJson.GetProperty("Status").GetString()),
            IsRunning = stateJson.GetProperty("Running").GetBoolean(),
            StartedAt = ParseDateTime(stateJson.GetProperty("StartedAt").GetString()),
            ExitCode = stateJson.GetProperty("ExitCode").GetInt32()
        };
    }
}
```

### Argument Builder (Proper Escaping)

```csharp
internal class ArgumentBuilder
{
    private readonly List<string> _args = new();
    
    public ArgumentBuilder Add(string arg)
    {
        _args.Add(arg);
        return this;
    }
    
    public ArgumentBuilder Add(string flag, string value)
    {
        _args.Add(flag);
        _args.Add(value);
        return this;
    }
    
    public override string ToString() => string.Join(" ", _args);
}

internal static string EscapeArg(string arg)
{
    // Proper shell escaping for Windows/Linux
    if (OperatingSystem.IsWindows())
        return EscapeWindowsArg(arg);
    else
        return EscapePosixArg(arg);
}
```

---

## Testing Strategy

### Unit Tests (With Mock)

```csharp
[Fact]
public async Task DeployInstance_CreatesContainer_Success()
{
    // Arrange
    var mockDockerClient = new Mock<IDockerClient>();
    mockDockerClient
        .Setup(x => x.CreateContainerAsync(It.IsAny<DockerContainerCreateOptions>(), default))
        .ReturnsAsync("container123");
    
    var provider = new DockerInstanceProvider(
        Options.Create(new DockerInstanceProviderOptions { ... }),
        Mock.Of<IDrasiConfigurationProvider>(),
        mockDockerClient.Object
    );
    
    // Act
    var result = await provider.DeployInstanceAsync(deploymentInfo);
    
    // Assert
    Assert.Equal(PlacementProviderRuntimeStatus.Deployed, result.Status);
    mockDockerClient.Verify(x => x.CreateContainerAsync(
        It.Is<DockerContainerCreateOptions>(o => o.ContainerName == "draas-test"),
        default
    ));
}
```

### Integration Tests (Real Docker)

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task DockerCliClient_RealDocker_WorksCorrectly()
{
    // Requires Docker installed
    var client = new DockerCliClient("docker");
    
    var containerId = await client.CreateContainerAsync(new DockerContainerCreateOptions
    {
        ContainerName = "test-container",
        ImageName = "alpine:latest",
        Command = ["sleep", "3600"]
    });
    
    try
    {
        await client.StartContainerAsync(containerId);
        var state = await client.GetContainerStateAsync(containerId);
        Assert.Equal(DockerContainerStatus.Running, state.Status);
    }
    finally
    {
        await client.RemoveContainerAsync(containerId, force: true);
    }
}
```

---

## Alternative: Docker.DotNet Library

### Evaluation

**Pros:**
- Official Docker library for .NET
- Type-safe API
- No CLI parsing
- Better performance (direct API calls)
- Well-maintained

**Cons:**
- External dependency (NuGet package)
- Requires Docker daemon socket access
- More complex setup
- Larger binary size

### When to Use
- Production deployments with high container churn
- When type safety is critical
- When performance matters (avoid process spawning)

### When CLI is OK
- Development/testing
- Low-volume scenarios
- Simple use cases
- When Docker SDK dependency is undesirable

---

## Decision

### Immediate Actions (This PR)
1. ✅ Create `IDockerClient` interface
2. ⏳ Implement `DockerCliClient` with improved CLI wrapping
3. ⏳ Refactor `DockerInstanceProvider` to use `IDockerClient`
4. ⏳ Add unit tests with mocked `IDockerClient`

### Future Considerations
- Evaluate Docker.DotNet for production use
- Implement `DockerSdkClient` as alternative
- Make client selection configurable
- Benchmark CLI vs SDK performance

### Non-Goals
- Don't prematurely optimize
- Don't add Docker.DotNet dependency until proven necessary
- Don't over-engineer the CLI client

---

## Summary

**Problem**: Direct Docker CLI usage is brittle and hard to test  
**Solution**: Create `IDockerClient` abstraction  
**Implementation**: Start with improved CLI client, consider SDK later  
**Benefit**: Testability, flexibility, type safety, maintainability

This abstraction solves the interop anti-pattern while keeping the door open for future improvements.
