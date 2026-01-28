# DRaaS - Drasi as a Service

**Multi-Instance Management Platform for Drasi Servers**

DRaaS provides a comprehensive control plane for managing multiple [Drasi](https://drasi.io/) server instances across different hosting platforms. It abstracts platform complexity, enabling teams to deploy and monitor Drasi instances without direct infrastructure management.

## Overview

DRaaS is a **platform-agnostic orchestration system** that manages the complete lifecycle of Drasi server instances. It provides:

- ✅ **Multi-Instance Management**: Create, configure, and manage multiple isolated Drasi instances
- ✅ **Platform Abstraction**: Deploy to Process (bare metal), Docker, or Azure Kubernetes Service (AKS)
- ✅ **Automatic Resource Allocation**: Platform managers determine their own resource requirements (host, port)
- ✅ **Bidirectional Status Monitoring**: Polling for local processes, push-based for distributed platforms
- ✅ **Configuration Management**: Full CRUD operations on Drasi server configurations (sources, queries, reactions)
- ✅ **Modular Architecture**: Reusable business logic library (Core) with pluggable interfaces (ControlPlane)

## Architecture

DRaaS follows **Clean Architecture** principles with clear separation between business logic and interface layers:

```mermaid
graph TB
    subgraph Reconciliation["DRaaS.Reconciliation (Worker Service)"]
        RecBG[ReconciliationBackgroundService]
        RecSvc[ReconciliationService]
        RecAPI[ReconciliationApiClient]
        RecBG -->|Periodic & Event Polling| RecSvc
        RecSvc -->|HTTP Requests| RecAPI
    end

    subgraph ControlPlane["DRaaS.ControlPlane (REST API)"]
        Controllers[Controllers]
        ServerCtrl[ServerController]
        ConfigCtrl[ConfigurationController]
        StatusCtrl[StatusController]
        Controllers --> ServerCtrl
        Controllers --> ConfigCtrl
        Controllers --> StatusCtrl
    end

    subgraph Core["DRaaS.Core (Business Logic)"]
        subgraph Services["Services"]
            Instance[Instance Management]
            Storage[Runtime Storage]
            Monitoring[Status Monitoring]
            Orchestration[Platform Orchestration]
            Resources[Resource Allocation]
            Factory[Manager Factory]
        end

        subgraph Providers["Providers"]
            Process[ProcessInstanceManager]
            Docker[DockerInstanceManager]
            AKS[AksInstanceManager]
        end

        Services --> Factory
        Factory --> Providers
    end

    RecAPI -->|HTTP/REST| Controllers
    Controllers -->|Uses| Services

    style Reconciliation fill:#e1f5ff
    style ControlPlane fill:#fff4e1
    style Core fill:#f0f0f0
```

### System Components

1. **DRaaS.Reconciliation** - Standalone worker service that ensures desired state convergence
   - Polls ControlPlane API for status changes
   - Detects configuration drift
   - Applies reconciliation strategies via API calls

2. **DRaaS.ControlPlane** - REST API layer exposing HTTP endpoints
   - Instance lifecycle operations (create, start, stop, restart, delete)
   - Configuration management (CRUD operations)
   - Status event streaming for reconciliation

3. **DRaaS.Core** - Reusable business logic library
   - Platform-agnostic services
   - Platform-specific managers
   - Domain models and interfaces

### Key Design Principles

1. **API-First Architecture**: All external components (Reconciliation) communicate through ControlPlane API
2. **Separation of Concerns**: Core (business logic) is independent of ControlPlane (Web API) and Reconciliation (Worker)
3. **Interface Segregation**: Small, focused interfaces for each responsibility
4. **Dependency Inversion**: Platform managers implement interfaces, orchestrator coordinates
5. **Open/Closed Principle**: Add new platforms or reconciliation strategies without modifying existing code
6. **Platform-Driven Resource Allocation**: Managers determine their own requirements

## Core Features

### 🎯 Multi-Instance Management

Create and manage multiple isolated Drasi instances:

```bash
POST /api/server/instances
{
  "name": "analytics-prod",
  "description": "Production analytics instance"
}
```

**Response**:
```json
{
  "instance": {
    "id": "abc-123",
    "name": "analytics-prod",
    "platformType": "Process",
    "status": "Created",
    "createdAt": "2025-01-28T10:00:00Z"
  },
  "serverConfiguration": {
    "host": "127.0.0.1",
    "port": 8080,
    "logLevel": "info"
  }
}
```

### 🖥️ Platform Abstraction

Deploy to three platforms with automatic selection and resource allocation:

| Platform | Description | Resource Allocation | Monitoring |
|----------|-------------|---------------------|------------|
| **Process** | Bare metal OS processes | `127.0.0.1` + allocated port | Polling (5s intervals) |
| **Docker** | Docker containers | `0.0.0.0` + allocated port | Push (daemon) |
| **AKS** | Kubernetes pods | `0.0.0.0:8080` (K8s service) | Push (daemon) |

Platform managers implement `IDrasiServerInstanceManager` and determine their own hosting parameters:

```csharp
public Task<ServerConfiguration> AllocateResourcesAsync(IPortAllocator portAllocator)
{
    // Process manager knows it needs localhost
    var port = portAllocator.AllocatePort();
    return new ServerConfiguration { Host = "127.0.0.1", Port = port };
}
```

### 📊 Bidirectional Status Monitoring

Two monitoring patterns based on platform architecture:

```mermaid
graph LR
    subgraph Polling["Polling Pattern (Process)"]
        PM[ProcessStatusMonitor]
        PM -->|Every 5s| Check[Check process.HasExited]
        Check --> SUS1[StatusUpdateService]
        SUS1 --> Buffer1[Status Change Buffer]
    end

    subgraph Push["Push Pattern (Docker/AKS)"]
        Daemon[External Daemon]
        Daemon -->|Monitors Events| Detect[Detect Status Change]
        Detect -->|POST /api/status/updates| SC[StatusController]
        SC --> SUS2[StatusUpdateService]
        SUS2 --> Buffer2[Status Change Buffer]
    end

    subgraph Reconciliation["Reconciliation Polling"]
        RecSvc[ReconciliationBackgroundService]
        RecSvc -->|GET /api/status/recent-changes| API[Status API]
        API -->|Returns filtered changes| RecSvc
    end

    Buffer1 --> API
    Buffer2 --> API

    style Polling fill:#e8f4f8
    style Push fill:#f8e8f4
    style Reconciliation fill:#e1f5ff
```

**Status Flow**:
1. **Local Process Monitoring**: `ProcessStatusMonitor` polls every 5s, publishes to `StatusUpdateService`
2. **Remote Daemon Monitoring**: External daemons POST status changes to `/api/status/updates`
3. **Buffer & API**: `StatusUpdateService` maintains rolling buffer (last 1000 changes)
4. **Reconciliation Polling**: `ReconciliationBackgroundService` polls `/api/status/recent-changes` for `ConfigurationChanged` events
5. **Event-Driven Reconciliation**: Immediate response to configuration changes via API polling

### ⚙️ Configuration Management

Full CRUD operations on Drasi configurations using **JSON Patch (RFC 6902)**:

```bash
PATCH /api/configuration/instances/abc-123
Content-Type: application/json-patch+json

[
  {
    "op": "add",
    "path": "/sources/-",
    "value": {
      "kind": "postgresql",
      "id": "my-db",
      "autoStart": true
    }
  },
  {
    "op": "add",
    "path": "/queries/-",
    "value": {
      "id": "active-users",
      "queryText": "MATCH (u:User) WHERE u.active = true RETURN u",
      "sources": [{ "sourceId": "my-db" }]
    }
  }
]
```

**Configuration Model** (matches Drasi server.yaml):

```yaml
host: 127.0.0.1
port: 8080
logLevel: info

sources:
  - kind: postgresql
    id: my-db
    autoStart: true

queries:
  - id: active-users
    queryText: "MATCH (u:User) WHERE u.active = true RETURN u"
    sources:
      - sourceId: my-db

reactions:
  - kind: webhook
    id: notify-slack
    queries: [active-users]
```

### 🔌 Modular Architecture

**DRaaS.Core** is a reusable library that can be consumed by any .NET application:

- **Web API** (current): ASP.NET Core REST API
- **Console App**: CLI tool for automation
- **Desktop App**: WPF/MAUI visual manager
- **Azure Function**: Serverless event-driven management
- **gRPC Service**: High-performance binary protocol

```csharp
// Console Application Example
var services = new ServiceCollection();
services.AddSingleton<IDrasiInstanceService, DrasiInstanceService>();
// ... register Core services

var serviceProvider = services.BuildServiceProvider();
var instanceService = serviceProvider.GetRequiredService<IDrasiInstanceService>();

var instance = await instanceService.CreateInstanceAsync("my-instance");
```

## Technology Stack

### Core Library
- **.NET 10.0** - Target framework
- **YamlDotNet 16.3.0** - YAML serialization for Drasi configs
- **Microsoft.AspNetCore.JsonPatch 10.0.2** - JSON Patch support

### Web API (ControlPlane)
- **ASP.NET Core 10.0** - Web framework
- **Microsoft.AspNetCore.Mvc.NewtonsoftJson** - Required for JsonPatch
- **Scalar.AspNetCore** - Modern OpenAPI documentation UI

## Project Structure

```
src/
├── DRaaS.Core/                        # Reusable business logic library
│   ├── Models/                        # Domain models
│   │   ├── Configuration.cs           # Drasi server configuration
│   │   ├── DrasiInstance.cs           # Instance metadata
│   │   ├── InstanceRuntimeInfo.cs     # Runtime state
│   │   ├── PlatformType.cs            # Enum: Process, Docker, AKS
│   │   ├── Query.cs, Source.cs, Reaction.cs
│   │   └── ServerConfiguration.cs     # Server settings
│   │
│   ├── Services/                      # Business logic services
│   │   ├── Instance/                  # Instance lifecycle
│   │   │   ├── IDrasiInstanceService.cs
│   │   │   └── DrasiInstanceService.cs
│   │   ├── Storage/                   # Runtime persistence
│   │   │   ├── IInstanceRuntimeStore.cs
│   │   │   └── InMemoryInstanceRuntimeStore.cs
│   │   ├── ResourceAllocation/        # Port management
│   │   │   ├── IPortAllocator.cs
│   │   │   └── PortAllocator.cs
│   │   ├── Monitoring/                # Status monitoring
│   │   │   ├── IStatusUpdateService.cs
│   │   │   ├── StatusUpdateService.cs
│   │   │   ├── IStatusMonitor.cs
│   │   │   └── ProcessStatusMonitor.cs
│   │   ├── Orchestration/             # Platform coordination
│   │   │   ├── IPlatformOrchestratorService.cs
│   │   │   └── PlatformOrchestratorService.cs
│   │   └── Factory/                   # Manager selection
│   │       ├── IInstanceManagerFactory.cs
│   │       └── InstanceManagerFactory.cs
│   │
│   ├── Providers/                     # Platform implementations
│   │   ├── IDrasiServerInstanceManager.cs
│   │   ├── IDrasiServerConfigurationProvider.cs
│   │   ├── DrasiServerConfigurationProvider.cs
│   │   └── InstanceManagers/
│   │       ├── ProcessInstanceManager.cs
│   │       ├── DockerInstanceManager.cs
│   │       └── AksInstanceManager.cs
│   │
│   ├── README.md                      # Core architecture documentation
│   ├── DISTRIBUTED_MONITORING.md      # Monitoring architecture
│   └── Services/README.md             # Service organization guide
│
└── DRaaS.ControlPlane/                # Web API frontend
    ├── Controllers/                   # REST API endpoints
    │   ├── ServerController.cs        # Instance management
    │   ├── ConfigurationController.cs # Configuration CRUD
    │   └── StatusController.cs        # Daemon status updates
    ├── DTOs/                          # API request/response models
    │   └── CreateInstanceRequest.cs
    ├── Program.cs                     # DI and startup
    └── README.md                      # API documentation
```

## Getting Started

### Prerequisites

- **.NET 10.0 SDK** (or later)

### Running the Web API

```bash
# Clone the repository
git clone https://github.com/yourusername/draas.git
cd draas

# Run the Web API
cd src/DRaaS.ControlPlane
dotnet run
```

**API Documentation**:
- Scalar UI: `http://localhost:5000/scalar/v1` (recommended)
- Swagger UI: `http://localhost:5000/swagger`

### Using DRaaS.Core in Your Application

#### 1. Reference the Core Library

```xml
<ItemGroup>
  <ProjectReference Include="..\DRaaS.Core\DRaaS.Core.csproj" />
</ItemGroup>
```

#### 2. Register Services

```csharp
using DRaaS.Core.Services.Instance;
using DRaaS.Core.Services.Storage;
using DRaaS.Core.Services.Orchestration;
using DRaaS.Core.Services.ResourceAllocation;
using DRaaS.Core.Services.Monitoring;
using DRaaS.Core.Services.Factory;
using DRaaS.Core.Providers.InstanceManagers;

var services = new ServiceCollection();

// Core services
services.AddSingleton<IPortAllocator, PortAllocator>();
services.AddSingleton<IInstanceRuntimeStore, InMemoryInstanceRuntimeStore>();
services.AddSingleton<IDrasiInstanceService, DrasiInstanceService>();
services.AddSingleton<IStatusUpdateService, StatusUpdateService>();

// Platform managers
services.AddSingleton<IDrasiServerInstanceManager, ProcessInstanceManager>();
services.AddSingleton<IDrasiServerInstanceManager, DockerInstanceManager>();

// Factory and orchestrator
services.AddSingleton<IInstanceManagerFactory, InstanceManagerFactory>();
services.AddSingleton<IPlatformOrchestratorService, PlatformOrchestratorService>();

var serviceProvider = services.BuildServiceProvider();
```

#### 3. Use the Services

```csharp
var instanceService = serviceProvider.GetRequiredService<IDrasiInstanceService>();

// Create instance
var instance = await instanceService.CreateInstanceAsync(
    name: "my-instance",
    description: "Test instance");

Console.WriteLine($"Created: {instance.Id} on {instance.PlatformType}");

// Get all instances
var instances = await instanceService.GetAllInstancesAsync();
foreach (var inst in instances)
{
    Console.WriteLine($"- {inst.Name} ({inst.Status})");
}
```

## API Reference

### Instance Management

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/server/instances` | POST | Create a new instance |
| `/api/server/instances` | GET | List all instances |
| `/api/server/instances/{id}` | GET | Get instance by ID |
| `/api/server/instances/{id}` | DELETE | Delete an instance |
| `/api/server/instances/{id}/start` | POST | Start an instance |
| `/api/server/instances/{id}/stop` | POST | Stop an instance |
| `/api/server/instances/{id}/restart` | POST | Restart an instance |
| `/api/server/instances/{id}/runtime-status` | GET | Get runtime status |
| `/api/server/platforms` | GET | List available platforms |

### Configuration Management

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/configuration/instances/{id}` | GET | Get full configuration |
| `/api/configuration/instances/{id}` | PATCH | Update configuration (JSON Patch) |
| `/api/server/instances/{id}/server-configuration` | GET | Get server settings only |
| `/api/server/instances/{id}/server-configuration` | PATCH | Update server settings |

### Status Monitoring

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/status/updates` | POST | Receive status update from daemon |
| `/api/status/{instanceId}/status` | GET | Get last known status |

## Monitoring Architecture

### Process Monitoring (Polling)

The control plane automatically monitors local processes:

```csharp
// Runs in background (Program.cs)
var statusMonitor = app.Services.GetRequiredService<IStatusMonitor>();
await statusMonitor.StartMonitoringAsync(applicationLifetime.ApplicationStopping);
```

Checks every **5 seconds**:
- Process still running?
- Exit code if stopped
- Publishes status changes to `IStatusUpdateService`

### Docker/AKS Monitoring (Push)

External daemons monitor distributed platforms and push updates:

**Docker Daemon Example**:
```csharp
// Daemon subscribes to Docker events
await foreach (var message in dockerClient.System.MonitorEventsAsync())
{
    var status = MapDockerEventToStatus(message.Status);

    // Push to control plane
    await httpClient.PostAsJsonAsync(
        "http://control-plane/api/status/updates",
        new {
            instanceId = message.Actor.Attributes["draas.instanceId"],
            status = status,
            source = "DockerDaemon"
        });
}
```

See [DISTRIBUTED_MONITORING.md](src/DRaaS.Core/DISTRIBUTED_MONITORING.md) for complete daemon implementation examples.

## Status Updates

Subscribe to status changes in your application:

```csharp
var statusService = serviceProvider.GetRequiredService<IStatusUpdateService>();

statusService.StatusChanged += (sender, e) =>
{
    Console.WriteLine($"[{e.Timestamp}] {e.InstanceId}");
    Console.WriteLine($"  {e.OldStatus} → {e.NewStatus}");
    Console.WriteLine($"  Source: {e.Source}");

    // Trigger actions:
    // - Send notifications (email, Slack, Teams)
    // - Log to external system
    // - Auto-restart on failure
    // - Update UI via SignalR
};
```

## Extensibility

### Adding a New Platform

1. **Implement the interface**:

```csharp
public class AzureContainerAppsManager : IDrasiServerInstanceManager
{
    public string PlatformType => "AzureContainerApps";

    public Task<ServerConfiguration> AllocateResourcesAsync(IPortAllocator portAllocator)
    {
        // Container Apps get their own URLs
        return new ServerConfiguration
        {
            Host = "0.0.0.0",
            Port = 80, // Container Apps handles routing
            LogLevel = "info"
        };
    }

    // Implement Start, Stop, Restart, GetStatus...
}
```

2. **Register in DI**:

```csharp
services.AddSingleton<IDrasiServerInstanceManager, AzureContainerAppsManager>();
```

3. **Update enum**:

```csharp
public enum PlatformType
{
    Process = 0,
    Docker = 1,
    AKS = 2,
    AzureContainerApps = 3  // New
}
```

### Adding Alternative Storage

Implement `IInstanceRuntimeStore` for distributed scenarios:

```csharp
public class CosmosDbInstanceRuntimeStore : IInstanceRuntimeStore
{
    private readonly CosmosClient _cosmosClient;
    private readonly Container _container;

    public async Task SaveAsync(InstanceRuntimeInfo runtimeInfo)
    {
        await _container.UpsertItemAsync(
            runtimeInfo,
            new PartitionKey(runtimeInfo.InstanceId));
    }

    // Implement Get, Delete, GetByPlatform...
}
```

**Register**:
```csharp
services.AddSingleton<IInstanceRuntimeStore, CosmosDbInstanceRuntimeStore>();
```

## Development Status

### ✅ Implemented

- ✅ Multi-instance management (create, read, update, delete)
- ✅ Platform abstraction (Process, Docker, AKS)
- ✅ Platform-driven resource allocation
- ✅ Automatic port allocation and tracking
- ✅ Bidirectional status monitoring (polling + push)
- ✅ Configuration management with JSON Patch
- ✅ YAML serialization for Drasi configs
- ✅ Event-driven status updates
- ✅ Modular service organization (6 domains)
- ✅ Clean architecture separation (Core + ControlPlane)
- ✅ OpenAPI documentation (Scalar)
- ✅ Comprehensive documentation

### 🔲 Planned Features

#### Platform Implementations
- 🔲 Actual process spawning (currently stubbed)
- 🔲 Docker container management (Docker SDK)
- 🔲 Kubernetes deployment management (K8s client)
- 🔲 Docker monitoring daemon
- 🔲 AKS monitoring daemon

#### Storage & Persistence
- 🔲 File-based configuration storage
- 🔲 Azure Cosmos DB runtime store
- 🔲 SQL Server runtime store
- 🔲 Redis runtime store

#### Advanced Orchestration
- 🔲 Load-based platform selection
- 🔲 Cost-based platform selection
- 🔲 Capability-based routing

#### Production Features
- 🔲 Authentication & authorization (JWT, API keys)
- 🔲 Role-based access control (RBAC)
- 🔲 Rate limiting
- 🔲 Request validation (FluentValidation)
- 🔲 Global error handling middleware
- 🔲 Structured logging (Serilog)
- 🔲 Health checks endpoint
- 🔲 Metrics (Prometheus)
- 🔲 SignalR for real-time UI updates
- 🔲 Webhook notifications

#### Testing
- 🔲 Unit tests for all services
- 🔲 Integration tests for API endpoints
- 🔲 End-to-end tests for instance lifecycle

## Documentation

- **[DRaaS.Core README](src/DRaaS.Core/README.md)** - Core architecture, usage examples, extensibility
- **[DRaaS.ControlPlane README](src/DRaaS.ControlPlane/README.md)** - Web API documentation, endpoints, DTOs
- **[Services Organization](src/DRaaS.Core/Services/README.md)** - Service layer structure, domains, dependencies
- **[Distributed Monitoring](src/DRaaS.Core/DISTRIBUTED_MONITORING.md)** - Monitoring architecture, daemon examples, K8s deployment

## Contributing

Contributions are welcome! Please follow these guidelines:

1. **Maintain separation of concerns**: Business logic in Core, interface in ControlPlane
2. **Use interfaces**: Depend on abstractions, not implementations
3. **Domain-driven organization**: Place services in appropriate domain folders
4. **Document architecture decisions**: Update relevant READMEs
5. **Follow existing patterns**: Platform managers, service structure, DI registration
6. **Write tests**: Unit tests for Core, integration tests for ControlPlane

## Design Principles

DRaaS follows these core principles:

1. **Separation of Concerns**: Core library independent of Web API
2. **Interface Segregation**: Small, focused interfaces
3. **Dependency Inversion**: Depend on abstractions
4. **Open/Closed Principle**: Extensible without modification
5. **Single Responsibility**: Each service has one clear purpose
6. **Platform-Driven Design**: Managers know their own requirements
7. **Event-Driven Architecture**: Status changes propagate via events

## License

[Your License Here]

## Acknowledgments

- [Drasi](https://drasi.io/) - The continuous intelligence platform we're orchestrating
- Clean Architecture principles by Robert C. Martin
- Domain-Driven Design patterns
