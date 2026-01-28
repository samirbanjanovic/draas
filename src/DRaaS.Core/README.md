# DRaaS.Core

**Drasi-as-a-Service Core Business Logic Library**

## Overview

DRaaS.Core contains the complete business logic, domain models, services, and providers for managing Drasi server instances across multiple platforms (Process, Docker, AKS). This library is **interface-agnostic** and can be consumed by any application type:

- Web APIs (ASP.NET Core)
- Console Applications
- Desktop Applications (WPF, WinForms, Avalonia)
- Mobile Applications (MAUI)
- Azure Functions
- Any other .NET application

## Architecture

```
DRaaS.Core/
├── Models/                          # Domain Models
│   ├── Configuration.cs             # Drasi server configuration
│   ├── DrasiInstance.cs             # Instance metadata
│   ├── InstanceRuntimeInfo.cs       # Runtime state information
│   ├── InstanceStatus.cs            # Instance status enum
│   ├── PlatformType.cs              # Platform type enum
│   ├── Query.cs, QuerySource.cs     # Query definitions
│   ├── Reaction.cs                  # Reaction definitions
│   ├── ServerConfiguration.cs       # Server settings (host, port, log level)
│   └── Source.cs                    # Data source definitions
│
├── Services/                        # Business Logic Services
│   ├── IDrasiInstanceService.cs     # Instance lifecycle management interface
│   ├── DrasiInstanceService.cs      # Instance lifecycle implementation
│   ├── IInstanceManagerFactory.cs   # Platform manager factory interface
│   ├── InstanceManagerFactory.cs    # Platform manager factory implementation
│   ├── IInstanceRuntimeStore.cs     # Runtime state storage interface
│   ├── InMemoryInstanceRuntimeStore.cs  # In-memory runtime storage
│   ├── IPlatformOrchestratorService.cs  # Platform selection & resource allocation interface
│   └── PlatformOrchestratorService.cs   # Platform orchestration implementation
│
└── Providers/                       # Platform Integration Providers
    ├── IDrasiServerConfigurationProvider.cs     # Configuration management interface
    ├── DrasiServerConfigurationProvider.cs      # Configuration management implementation
    ├── IDrasiServerInstanceManager.cs           # Platform manager interface
    └── InstanceManagers/
        ├── ProcessInstanceManager.cs            # Bare metal process management
        ├── DockerInstanceManager.cs             # Docker container management
        └── AksInstanceManager.cs                # Azure Kubernetes Service management
```

## Key Components

### 1. **Models** (Domain Layer)
Pure domain models with no external dependencies. These represent the core business entities:

- **DrasiInstance**: Metadata for a Drasi server instance (ID, name, platform, status)
- **Configuration**: Complete server configuration (sources, queries, reactions)
- **ServerConfiguration**: Server-specific settings (host, port, log level)
- **InstanceRuntimeInfo**: Runtime state (container IDs, process IDs, pod names, etc.)

### 2. **Services** (Business Logic Layer)
Core business logic that orchestrates instance and configuration management:

- **DrasiInstanceService**: CRUD operations for instance metadata
- **PlatformOrchestratorService**: Automatic platform selection and resource allocation
- **InstanceManagerFactory**: Returns appropriate platform manager based on PlatformType
- **IInstanceRuntimeStore**: Abstraction for storing runtime state (pluggable: InMemory, Cosmos, SQL, Redis)

### 3. **Providers** (Integration Layer)
Platform-specific implementations for launching and managing Drasi servers:

- **ProcessInstanceManager**: Manages bare metal OS processes
  - Launches drasi-server as a local process
  - Generates YAML configuration files from stored Configuration objects
  - Configurable via `ProcessInstanceManagerOptions` (executable path, directories, timeouts)
  - Monitors process health via polling
- **DockerInstanceManager**: Manages Docker containers
- **AksInstanceManager**: Manages Kubernetes deployments in AKS
- **DrasiServerConfigurationProvider**: Manages configuration files (YAML serialization)

#### ProcessInstanceManager Configuration

The `ProcessInstanceManager` requires configuration to locate the drasi-server executable and manage runtime files. Use `ProcessInstanceManagerOptions`:

```csharp
// In dependency injection setup
builder.Services.Configure<ProcessInstanceManagerOptions>(
    builder.Configuration.GetSection("ProcessInstanceManager"));
```

**Configuration Options** (`appsettings.json`):
```json
{
  "ProcessInstanceManager": {
    "ExecutablePath": "drasi-server",
    "InstanceConfigDirectory": "./drasi-configs",
    "DefaultLogLevel": "info",
    "ShutdownTimeoutSeconds": 5,
    "WorkingDirectory": "./drasi-runtime"
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ExecutablePath` | string | `"drasi-server"` | Path to drasi-server binary (absolute, relative, or in PATH) |
| `InstanceConfigDirectory` | string | `"./drasi-configs"` | Directory for instance-specific YAML configs |
| `DefaultLogLevel` | string | `"info"` | Default log level (trace, debug, info, warn, error) |
| `ShutdownTimeoutSeconds` | int | `5` | Graceful shutdown timeout before force kill |
| `WorkingDirectory` | string | `"./drasi-runtime"` | Working directory for processes |

When starting an instance, `ProcessInstanceManager`:
1. Receives the `Configuration` (sources, queries, reactions) from the configuration store
2. Generates a drasi-server YAML config file: `{InstanceConfigDirectory}/{instanceId}-config.yaml`
3. Launches the process: `drasi-server --config {configFile}`
4. Tracks the process with PID in `InstanceRuntimeInfo`
5. Cleans up config files when instances are stopped

**See Also**:
- [ProcessInstanceManager Configuration Guide](Providers/InstanceManagers/ProcessInstanceManager-README.md)
- [drasi-server Documentation](https://github.com/samirbanjanovic/drasi-server)

## Usage Examples

### Example 1: Console Application

```csharp
using DRaaS.Core.Services;
using DRaaS.Core.Providers;
using DRaaS.Core.Models;

// Setup DI
var services = new ServiceCollection();

// Register core services
services.AddSingleton<IPlatformOrchestratorService, PlatformOrchestratorService>();
services.AddSingleton<IInstanceRuntimeStore, InMemoryInstanceRuntimeStore>();
services.AddSingleton<IDrasiInstanceService, DrasiInstanceService>();

// Register platform managers
services.AddSingleton<IDrasiServerInstanceManager, ProcessInstanceManager>();
services.AddSingleton<IInstanceManagerFactory, InstanceManagerFactory>();

var provider = services.BuildServiceProvider();

// Create and start an instance
var instanceService = provider.GetRequiredService<IDrasiInstanceService>();
var instance = await instanceService.CreateInstanceAsync("my-drasi-instance");

var managerFactory = provider.GetRequiredService<IInstanceManagerFactory>();
var manager = managerFactory.GetManager(instance.PlatformType);

var runtimeInfo = await manager.StartInstanceAsync(instance.Id, configuration);
Console.WriteLine($"Instance started: {runtimeInfo.ProcessId}");
```

### Example 2: Desktop Application (WPF/MAUI)

```csharp
public class DrasiInstanceViewModel
{
    private readonly IDrasiInstanceService _instanceService;
    private readonly IInstanceManagerFactory _managerFactory;

    public DrasiInstanceViewModel(
        IDrasiInstanceService instanceService,
        IInstanceManagerFactory managerFactory)
    {
        _instanceService = instanceService;
        _managerFactory = managerFactory;
    }

    public async Task CreateAndStartInstance(string name)
    {
        // Create instance
        var instance = await _instanceService.CreateInstanceAsync(name);
        
        // Start it on the appropriate platform
        var manager = _managerFactory.GetManager(instance.PlatformType);
        await manager.StartInstanceAsync(instance.Id, config);
        
        // Update UI
        OnInstanceCreated(instance);
    }
}
```

### Example 3: Azure Function

```csharp
[Function("CreateDrasiInstance")]
public async Task<HttpResponseData> Run(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
    FunctionContext executionContext)
{
    var instanceService = executionContext.InstanceServices.GetRequiredService<IDrasiInstanceService>();
    var request = await req.ReadFromJsonAsync<CreateInstanceRequest>();
    
    var instance = await instanceService.CreateInstanceAsync(request.Name);
    
    var response = req.CreateResponse(HttpStatusCode.Created);
    await response.WriteAsJsonAsync(instance);
    return response;
}
```

## Extensibility

### Custom Storage Implementation

Implement `IInstanceRuntimeStore` to use your preferred storage:

```csharp
public class CosmosInstanceRuntimeStore : IInstanceRuntimeStore
{
    private readonly CosmosClient _cosmosClient;
    
    public CosmosInstanceRuntimeStore(CosmosClient cosmosClient)
    {
        _cosmosClient = cosmosClient;
    }
    
    public async Task SaveAsync(InstanceRuntimeInfo runtimeInfo)
    {
        var container = _cosmosClient.GetContainer("draas", "runtime");
        await container.UpsertItemAsync(runtimeInfo, new PartitionKey(runtimeInfo.InstanceId));
    }
    
    // Implement other methods...
}
```

### Custom Platform Manager

Implement `IDrasiServerInstanceManager` for new platforms:

```csharp
public class AzureContainerAppsManager : IDrasiServerInstanceManager
{
    public string PlatformType => "ACA";
    
    public async Task<InstanceRuntimeInfo> StartInstanceAsync(string instanceId, Configuration configuration)
    {
        // Use Azure Container Apps SDK to deploy
        // Return runtime info
    }
    
    // Implement other methods...
}
```

## Dependencies

- **YamlDotNet**: YAML serialization/deserialization
- **Microsoft.AspNetCore.JsonPatch**: JSON Patch support for configuration updates
- **.NET 10**: Target framework

## Design Principles

1. **Separation of Concerns**: Clear boundaries between models, services, and providers
2. **Dependency Inversion**: All dependencies point inward (interfaces in Core, implementations pluggable)
3. **Interface Segregation**: Small, focused interfaces (IInstanceRuntimeStore, IDrasiServerInstanceManager, etc.)
4. **Open/Closed**: Extensible through new implementations without modifying existing code
5. **Single Responsibility**: Each class has one reason to change

## Testing

The architecture makes testing straightforward:

```csharp
[Fact]
public async Task CreateInstance_ShouldAllocateResources()
{
    // Arrange
    var mockOrchestrator = new Mock<IPlatformOrchestratorService>();
    var mockStore = new Mock<IInstanceRuntimeStore>();
    var service = new DrasiInstanceService(mockOrchestrator.Object);
    
    // Act
    var instance = await service.CreateInstanceAsync("test-instance");
    
    // Assert
    Assert.NotNull(instance.Id);
    Assert.Equal("test-instance", instance.Name);
}
```

## License

[Your License Here]

## Contributing

[Contributing Guidelines]
