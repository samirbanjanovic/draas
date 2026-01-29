# DRaaS - Drasi as a Service

**Multi-Instance Management Platform for Drasi Servers**

DRaaS provides a comprehensive control plane for managing multiple [Drasi](https://drasi.io/) server instances across different hosting platforms. It abstracts platform complexity, enabling teams to deploy and monitor Drasi instances without direct infrastructure management.

## Overview

DRaaS is a distributed orchestration system that manages the complete lifecycle of Drasi server instances. It provides:

- Multi-Instance Management: Create, configure, and manage multiple isolated Drasi instances
- Platform Abstraction: Deploy to Process (bare metal), Docker, or Azure Kubernetes Service (AKS)
- Message Bus Architecture: Distributed communication via Redis Pub/Sub for command routing and event broadcasting
- Worker-Based Execution: Platform-specific worker services handle actual instance management
- Configuration Management: Full CRUD operations on Drasi server configurations (sources, queries, reactions)
- Modular Architecture: Reusable business logic library (Core) with pluggable interfaces (ControlPlane, Workers)

## Architecture

DRaaS uses a message bus architecture with clear separation between API, workers, and business logic:

```mermaid
graph TB
    subgraph MessageBus["Redis Message Bus"]
        CmdChannels[Command Channels<br/>instance.commands.*]
        EventChannels[Event Channels<br/>instance.events]
    end

    subgraph Workers["Platform Workers"]
        ProcessWorker[DRaaS.Workers.Platform.Process]
        DockerWorker[DRaaS.Workers.Platform.Docker]
        AKSWorker[DRaaS.Workers.Platform.AKS]
    end

    subgraph Reconciliation["DRaaS.Reconciliation"]
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
        subgraph Messaging["Messaging"]
            IMessageBus[IMessageBus]
            RedisMessageBus[RedisMessageBus]
            Channels[Channels]
            Commands[Commands]
            Events[Events]
        end

        subgraph Services["Services"]
            Instance[Instance Management]
            Storage[Runtime Storage]
            Monitoring[Status Monitoring]
            Orchestration[Platform Orchestration]
            Resources[Resource Allocation]
            Factory[Manager Factory]
        end

        subgraph Providers["Providers"]
            ProcessMgr[ProcessInstanceManager]
            DockerMgr[DockerInstanceManager]
            AKSMgr[AksInstanceManager]
        end

        Services --> Factory
        Factory --> Providers
    end

    Controllers -->|Publish Commands| CmdChannels
    CmdChannels -->|Subscribe| ProcessWorker
    CmdChannels -->|Subscribe| DockerWorker
    CmdChannels -->|Subscribe| AKSWorker

    ProcessWorker -->|Publish Events| EventChannels
    DockerWorker -->|Publish Events| EventChannels
    AKSWorker -->|Publish Events| EventChannels

    EventChannels -.->|Optional Subscribe| Reconciliation
    RecAPI -->|HTTP/REST| Controllers
    Controllers -->|Uses| Services

    style MessageBus fill:#ffe6e6
    style Workers fill:#e8f8e8
    style Reconciliation fill:#e1f5ff
    style ControlPlane fill:#fff4e1
    style Core fill:#f0f0f0
```

### System Components

1. **DRaaS.Core** - Reusable business logic library
   - Domain models and interfaces
   - Platform-agnostic services
   - Message bus infrastructure (IMessageBus, RedisMessageBus)
   - Command and event message definitions
   - Platform manager interfaces

2. **DRaaS.ControlPlane** - REST API layer exposing HTTP endpoints
   - Instance lifecycle operations (create, start, stop, restart, delete)
   - Configuration management (CRUD operations)
   - Publishes commands to message bus channels
   - Receives status updates from external daemons
   - Status event streaming for reconciliation

3. **DRaaS.Workers.Platform.*** - Platform-specific worker services
   - Subscribe to command channels (e.g., `instance.commands.process`)
   - Execute platform-specific operations (launch processes, manage containers)
   - Monitor platform health and status
   - Publish events to broadcast channels (e.g., `instance.events`)
   - Can run on separate machines from ControlPlane

4. **DRaaS.Reconciliation** - Standalone worker service for desired state convergence
   - Polls ControlPlane API for status changes
   - Detects configuration drift
   - Applies reconciliation strategies via API calls
   - Can optionally subscribe to message bus events

5. **Redis Message Bus** - Communication infrastructure
   - Platform-specific command channels for routing
   - Broadcast event channels for notifications
   - Request/response pattern with temporary reply channels
   - Enables distributed worker deployment

### Key Design Principles

1. **Message Bus Architecture**: Commands and events flow through Redis Pub/Sub channels for decoupled communication
2. **Distributed Execution**: Platform workers run independently, enabling deployment across multiple machines
3. **API-First**: ControlPlane is the central API, workers are execution engines
4. **Channel-Based Routing**: Platform-specific channels allow independent scaling (process, docker, aks)
5. **Event-Driven Monitoring**: Workers publish events that subscribers can react to
6. **Request/Response Pattern**: Synchronous operations use temporary reply channels
7. **Separation of Concerns**: API (ControlPlane), Logic (Core), Execution (Workers), Convergence (Reconciliation)

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

### Platform Abstraction

Deploy to three platforms with worker-based execution:

| Platform | Description | Worker Service | Communication | Monitoring |
|----------|-------------|----------------|---------------|------------|
| **Process** | Bare metal OS processes | DRaaS.Workers.Platform.Process | Message bus | Worker polls every 10s |
| **Docker** | Docker containers | DRaaS.Workers.Platform.Docker | Message bus | External daemon |
| **AKS** | Kubernetes pods | DRaaS.Workers.Platform.AKS | Message bus | External daemon |

Platform workers subscribe to their specific command channels:
- `instance.commands.process` - Process platform commands
- `instance.commands.docker` - Docker platform commands
- `instance.commands.aks` - AKS platform commands

Workers publish events to shared channels:
- `instance.events` - Instance lifecycle events (started, stopped, failed)
- `configuration.events` - Configuration change notifications
- `status.events` - Status update events

#### Process Platform Configuration

ProcessInstanceManager is used by the DRaaS.Workers.Platform.Process service. Configure in worker's `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  },
  "ProcessInstanceManager": {
    "ExecutablePath": "drasi-server",
    "InstanceConfigDirectory": "./drasi-configs",
    "DefaultLogLevel": "info",
    "ShutdownTimeoutSeconds": 5,
    "WorkingDirectory": "./drasi-runtime"
  }
}
```

When creating an instance with `platformType: "Process"`:

1. API publishes StartInstanceCommand to `instance.commands.process` channel
2. ProcessCommandWorker (in DRaaS.Workers.Platform.Process) receives command via Redis
3. Worker generates drasi-server YAML config file from Configuration (sources, queries, reactions)
4. Worker saves it to `{InstanceConfigDirectory}/{instanceId}-config.yaml`
5. Worker launches: `drasi-server --config {configFile}`
6. Worker tracks the process with PID
7. ProcessMonitorWorker continuously polls process health (every 10s)
8. Worker publishes InstanceStartedEvent to `instance.events` channel

**Example Generated YAML**:
```yaml
id: my-instance
host: 127.0.0.1
port: 8080
logLevel: info
persistConfig: true
persistIndex: false

sources:
  - kind: postgres
    id: my-db
    autoStart: true

queries:
  - id: my-query
    query: |
      MATCH (n) RETURN n
    sources:
      - sourceId: my-db

reactions:
  - kind: log
    id: log-output
    queries: [my-query]
```

See:
- [DRaaS.Workers.Platform.Process README](src/DRaaS.Workers.Platform.Process/README.md)
- [ProcessInstanceManager Configuration Guide](src/DRaaS.Core/Providers/InstanceManagers/ProcessInstanceManager-README.md)
- [Example YAML Configuration](src/DRaaS.ControlPlane/drasi-server-config-example.yaml)
- [drasi-server Documentation](https://github.com/samirbanjanovic/drasi-server)

### Status Monitoring

Multiple monitoring patterns based on platform architecture:

```mermaid
graph LR
    subgraph WorkerBased["Worker-Based (Process)"]
        PMW[ProcessMonitorWorker]
        PMW -->|Polls every 10s| Check[Check process.HasExited]
        Check -->|Publish Event| MB1[Message Bus<br/>instance.events]
    end

    subgraph DaemonBased["Daemon-Based (Docker/AKS)"]
        Daemon[External Daemon]
        Daemon -->|Monitors Events| Detect[Detect Status Change]
        Detect -->|POST /api/status/updates| SC[StatusController]
        SC --> SUS[StatusUpdateService]
        SUS --> Buffer[Status Change Buffer]
    end

    subgraph Reconciliation["Reconciliation"]
        RecSvc[ReconciliationBackgroundService]
        RecSvc -->|GET /api/status/recent-changes| API[Status API]
        RecSvc -.->|Optional Subscribe| MB1
    end

    MB1 -.->|Broadcast| Subscribers[Event Subscribers]
    Buffer --> API

    style WorkerBased fill:#e8f8e8
    style DaemonBased fill:#e8f4f8
    style Reconciliation fill:#e1f5ff
```

**Status Flow**:
1. **Process Platform**: ProcessMonitorWorker polls every 10s, publishes InstanceStatusChangedEvent to message bus
2. **Docker/AKS Platforms**: External daemons POST status changes to `/api/status/updates`
3. **Event Distribution**: Message bus broadcasts events to all subscribers (reconciliation, logging, monitoring)
4. **Status Buffer**: StatusUpdateService maintains rolling buffer (last 1000 changes) accessible via API
5. **Reconciliation**: Can poll API `/api/status/recent-changes` and/or subscribe to message bus events

### Configuration Management

Full CRUD operations on Drasi configurations using JSON Patch (RFC 6902):

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

### Modular Architecture

DRaaS.Core is a reusable library that can be consumed by any .NET application:

- Web API (current): ASP.NET Core REST API
- Console App: CLI tool for automation
- Desktop App: WPF/MAUI visual manager
- Azure Function: Serverless event-driven management
- gRPC Service: High-performance binary protocol

```csharp
// Console Application Example
var services = new ServiceCollection();

// Register message bus
var redisConnection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
services.AddSingleton<IConnectionMultiplexer>(redisConnection);
services.AddSingleton<IMessageBus, RedisMessageBus>();

// Register services
services.AddSingleton<IDrasiInstanceService, DrasiInstanceService>();
// ... register other Core services

var serviceProvider = services.BuildServiceProvider();
var instanceService = serviceProvider.GetRequiredService<IDrasiInstanceService>();
var messageBus = serviceProvider.GetRequiredService<IMessageBus>();

// Create instance (metadata only)
var instance = await instanceService.CreateInstanceAsync("my-instance");

// Publish command to start instance (executed by worker)
await messageBus.PublishAsync(
    Channels.GetInstanceCommandChannel(instance.PlatformType),
    new StartInstanceCommand { InstanceId = instance.Id, Configuration = config });
```

## Technology Stack

### Core Library
- **.NET 10.0** - Target framework
- **StackExchange.Redis** - Redis client for message bus
- **YamlDotNet 16.3.0** - YAML serialization for Drasi configs
- **Microsoft.AspNetCore.JsonPatch 10.0.2** - JSON Patch support

### Web API (ControlPlane)
- **ASP.NET Core 10.0** - Web framework
- **StackExchange.Redis** - Redis client
- **Microsoft.AspNetCore.Mvc.NewtonsoftJson** - Required for JsonPatch
- **Scalar.AspNetCore** - Modern OpenAPI documentation UI

### Worker Services
- **.NET 10.0** - Worker Service framework
- **StackExchange.Redis** - Message bus client
- **DRaaS.Core** - Business logic and messaging contracts

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
│   ├── Messaging/                     # Message bus infrastructure
│   │   ├── IMessageBus.cs             # Message bus abstraction
│   │   ├── RedisMessageBus.cs         # Redis Pub/Sub implementation
│   │   ├── Channels.cs                # Channel name constants
│   │   ├── Messages.cs                # Base message types
│   │   ├── Commands/                  # Command messages
│   │   │   ├── InstanceCommands.cs    # Start, Stop, Restart, Delete
│   │   │   └── ConfigurationCommands.cs
│   │   ├── Events/                    # Event messages
│   │   │   ├── InstanceEvents.cs      # Lifecycle events
│   │   │   └── ConfigurationEvents.cs
│   │   └── Responses/                 # Response messages
│   │       └── InstanceCommandResponses.cs
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
│   │   ├── Reconciliation/            # Desired state reconciliation interfaces
│   │   │   ├── IReconciliationService.cs
│   │   │   ├── IConfigurationStateStore.cs
│   │   │   ├── ReconciliationStrategy.cs
│   │   │   ├── DriftDetectionResult.cs
│   │   │   └── ReconciliationOptions.cs
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
├── DRaaS.ControlPlane/                # Web API frontend
│   ├── Controllers/                   # REST API endpoints
│   │   ├── ServerController.cs        # Instance management
│   │   ├── ConfigurationController.cs # Configuration CRUD
│   │   └── StatusController.cs        # Daemon status updates
│   ├── DTOs/                          # API request/response models
│   │   └── CreateInstanceRequest.cs
│   ├── Program.cs                     # DI and startup
│   └── README.md                      # API documentation
│
├── DRaaS.Workers.Platform.Process/    # Process platform worker
│   ├── ProcessCommandWorker.cs        # Command handler
│   ├── ProcessMonitorWorker.cs        # Health monitoring
│   ├── ProcessInstanceManager.cs      # Process management
│   ├── Program.cs                     # Worker startup
│   └── README.md                      # Worker documentation
│
├── DRaaS.Workers.Platform.Docker/     # Docker platform worker (planned)
│   └── README.md
│
├── DRaaS.Workers.Platform.AKS/        # AKS platform worker (planned)
│   └── README.md
│
└── DRaaS.Reconciliation/              # Reconciliation service
    ├── ReconciliationBackgroundService.cs  # Background worker
    ├── ReconciliationService.cs       # Drift detection
    ├── Strategies/                    # Reconciliation strategies
    │   ├── IReconciliationStrategy.cs
    │   └── RestartReconciliationStrategy.cs
    ├── ReconciliationApiClient.cs     # HTTP API client
    ├── ConfigurationStateStore.cs     # State tracking
    ├── Program.cs                     # Service startup
    └── README.md                      # Reconciliation documentation
```

## Getting Started

### Prerequisites

- **.NET 10.0 SDK** (or later)
- **Redis** (localhost or remote instance)
- **drasi-server** executable (for Process platform)

### Running the System

The complete system requires multiple services:

#### 1. Start Redis

```bash
# Using Docker
docker run -d -p 6379:6379 redis:latest

# Or install locally
# Windows: https://redis.io/docs/getting-started/installation/install-redis-on-windows/
# Linux: sudo apt-get install redis-server
```

#### 2. Run the ControlPlane API

```bash
cd src/DRaaS.ControlPlane
dotnet run
```

**API Documentation**:
- Scalar UI: `http://localhost:5000/scalar/v1` (recommended)
- Swagger UI: `http://localhost:5000/swagger`

#### 3. Run Platform Workers

For Process platform support:

```bash
cd src/DRaaS.Workers.Platform.Process
dotnet run
```

The worker will:
- Connect to Redis message bus
- Subscribe to `instance.commands.process` channel
- Monitor running processes every 10 seconds
- Publish events to `instance.events` channel

#### 4. (Optional) Run Reconciliation Service

```bash
cd src/DRaaS.Reconciliation
dotnet run
```

### Using DRaaS.Core in Your Application

#### 1. Reference the Core Library

```xml
<ItemGroup>
  <ProjectReference Include="..\DRaaS.Core\DRaaS.Core.csproj" />
  <PackageReference Include="StackExchange.Redis" Version="2.8.16" />
</ItemGroup>
```

#### 2. Register Services

```csharp
using DRaaS.Core.Messaging;
using DRaaS.Core.Services.Instance;
using DRaaS.Core.Services.Storage;

using DRaaS.Core.Services.Orchestration;
using DRaaS.Core.Services.ResourceAllocation;
using DRaaS.Core.Services.Monitoring;
using DRaaS.Core.Services.Factory;
using DRaaS.Core.Providers.InstanceManagers;
using StackExchange.Redis;

var services = new ServiceCollection();

// Redis connection for message bus
var redisConnection = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
services.AddSingleton<IConnectionMultiplexer>(redisConnection);

// Message bus
services.AddSingleton<IMessageBus, RedisMessageBus>();

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
var messageBus = serviceProvider.GetRequiredService<IMessageBus>();

// Create instance (metadata only)
var instance = await instanceService.CreateInstanceAsync(
    name: "my-instance",
    description: "Test instance");

Console.WriteLine($"Created: {instance.Id} on {instance.PlatformType}");

// Publish command to start instance (worker will execute)
await messageBus.PublishAsync(
    Channels.GetInstanceCommandChannel(instance.PlatformType),
    new StartInstanceCommand 
    { 
        InstanceId = instance.Id, 
        Configuration = config 
    });

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

### Implemented

- Multi-instance management (create, read, update, delete)
- Platform abstraction (Process, Docker, AKS interfaces)
- Message bus architecture with Redis Pub/Sub
- Worker-based execution for Process platform
- Request/response pattern with temporary reply channels
- Event broadcasting via message bus channels
- Automatic port allocation and tracking
- Worker-based status monitoring (polling every 10s)
- Configuration management with JSON Patch
- YAML serialization for Drasi configs
- Modular service organization (7 domains including Messaging)
- Separation of API, Core, and Workers
- OpenAPI documentation (Scalar)
- Comprehensive documentation

### Planned Features

#### Platform Implementations
- Docker container management worker (Docker SDK integration)
- Kubernetes deployment worker (K8s client integration)
- Docker monitoring daemon
- AKS monitoring daemon

#### Storage & Persistence
- File-based configuration storage
- Azure Cosmos DB runtime store
- SQL Server runtime store
- Redis runtime store
- Distributed state management across workers

#### Message Bus Enhancements
- Dead letter queue for failed message processing
- Message replay capability
- Distributed tracing integration
- Message audit trail
- Webhook subscriptions for message bus events

#### Production Features
- Authentication & authorization (JWT, API keys)
- Role-based access control (RBAC) for API and message bus
- Rate limiting on API and message publishing
- Request validation (FluentValidation)
- Global error handling middleware
- Structured logging (Serilog)
- Health checks endpoint
- Metrics (Prometheus)
- SignalR for real-time UI updates
- Webhook notifications

#### Testing
- Unit tests for all services and message handlers
- Integration tests for API endpoints and message bus
- End-to-end tests for instance lifecycle with workers
- Load testing for message bus scalability

## Documentation

This repository contains comprehensive documentation organized by component. Below are abstracts of each guide with links for deep dives.

### 📖 Documentation Structure

```mermaid
graph TB
    Root[README.md<br/>Project Overview]

    subgraph Core["DRaaS.Core Documentation"]
        CoreReadme[Core/README.md<br/>Business Logic Library]
        ServicesReadme[Core/Services/README.md<br/>Service Organization]
        MonitoringDoc[Core/DISTRIBUTED_MONITORING.md<br/>Monitoring Architecture]
        ProcessDoc[Core/.../ProcessInstanceManager-README.md<br/>Process Manager Config]
    end

    subgraph API["API Layer Documentation"]
        APIReadme[ControlPlane/README.md<br/>REST API Guide]
        YAMLExample[ControlPlane/drasi-server-config-example.yaml<br/>Config Template]
    end

    subgraph Reconciliation["Reconciliation Documentation"]
        ReconcileReadme[Reconciliation/README.md<br/>Worker Service Architecture]
    end

    Root --> CoreReadme
    Root --> APIReadme
    Root --> ReconcileReadme

    CoreReadme --> ServicesReadme
    CoreReadme --> MonitoringDoc
    CoreReadme --> ProcessDoc

    APIReadme --> YAMLExample

    style Root fill:#e1f5ff
    style Core fill:#f0f0f0
    style API fill:#fff4e1
    style Reconciliation fill:#e8f8e8
```

---

### 📘 Core Documentation

#### [DRaaS.Core README](src/DRaaS.Core/README.md)
**Business Logic Library Architecture**

The Core library contains all domain models, services, message bus infrastructure, and platform providers. This guide covers:
- **Architecture Overview**: Models, Messaging, Services, and Providers layers
- **Message Bus**: IMessageBus interface, RedisMessageBus implementation, Channels, Commands, Events
- **Usage Examples**: Console apps, desktop apps, Azure Functions
- **Extensibility**: Custom storage implementations, new platform managers
- **Dependencies**: StackExchange.Redis, YamlDotNet, JsonPatch, .NET 10

**Key Topics**: Domain models (DrasiInstance, Configuration), Messaging infrastructure (Commands, Events, Channels), Service layer (Instance, Orchestration, Storage, Monitoring), Platform providers (Process, Docker, AKS)

---

#### [Services Organization Guide](src/DRaaS.Core/Services/README.md)
**Service Layer Structure and Patterns**

Explains the seven service domains and their responsibilities:
- **Instance Management**: Lifecycle operations (create, start, stop, delete)
- **Storage**: Runtime state persistence (in-memory, Cosmos, SQL, Redis)
- **Resource Allocation**: Port management and allocation
- **Monitoring**: Status tracking and event publishing
- **Orchestration**: Platform selection and resource coordination
- **Reconciliation**: Desired state drift detection and correction interfaces
- **Factory**: Platform manager instantiation

**Key Topics**: Domain boundaries, service dependencies, cross-cutting concerns, testing strategies

---

#### [Distributed Monitoring Architecture](src/DRaaS.Core/DISTRIBUTED_MONITORING.md)
**Status Monitoring Patterns and Implementation**

Comprehensive guide to the monitoring system:
- **Worker-Based Pattern**: ProcessMonitorWorker in separate service (polls every 10s)
- **Daemon-Based Pattern**: External daemons for Docker/AKS (event-driven)
- **Message Bus Events**: InstanceStatusChangedEvent published to `instance.events` channel
- **Status Event Bus**: IStatusUpdateService with centralized event publishing
- **Daemon Implementation**: Complete Docker and Kubernetes daemon examples
- **API Polling**: Reconciliation integration via `/api/status/recent-changes`

**Key Topics**: Worker-based monitoring, message bus events, daemon architecture, Kubernetes deployment, status buffering

---

#### [ProcessInstanceManager Configuration](src/DRaaS.Core/Providers/InstanceManagers/ProcessInstanceManager-README.md)
**Process Platform Configuration Guide**

Detailed guide for configuring the Process instance manager:
- **Configuration Options**: ExecutablePath, directories, timeouts, log levels
- **YAML Generation**: How Configuration objects become drasi-server YAML
- **Process Lifecycle**: Launch, monitoring, graceful shutdown, cleanup
- **Prerequisites**: drasi-server binary, permissions, port availability
- **Troubleshooting**: Common issues and solutions

**Key Topics**: appsettings.json configuration, YAML structure, process management, file paths

**Example Config**:
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

---

### 🌐 API Documentation

#### [DRaaS.ControlPlane README](src/DRaaS.ControlPlane/README.md)
**REST API Frontend Layer**

The Web API layer that exposes DRaaS.Core through HTTP endpoints:
- **API Endpoints**: Instance management, configuration CRUD, status updates
- **Message Bus Integration**: Publishes commands to Redis channels for worker execution
- **Architecture Flow**: API → Message Bus → Worker sequence diagrams
- **Request/Response Examples**: Complete API usage examples
- **Dependency Injection**: Service and message bus registration
- **OpenAPI Documentation**: Scalar and Swagger UI integration
- **Error Handling**: HTTP status codes and error patterns

**Key Topics**: REST endpoints, DTOs, controller patterns, JSON Patch, message bus publishing, status polling endpoint

**API Highlights**:
- `POST /api/server/instances` - Create instance (publishes StartInstanceCommand)
- `PATCH /api/configuration/instances/{id}` - Update configuration (JSON Patch)
- `GET /api/status/recent-changes` - Poll status events (reconciliation)
- `POST /api/status/updates` - Receive daemon updates

---

### Platform Workers Documentation

#### [DRaaS.Workers.Platform.Process README](src/DRaaS.Workers.Platform.Process/README.md)
**Process Platform Worker Service**

Worker service that manages local drasi-server processes:
- **ProcessCommandWorker**: Subscribes to `instance.commands.process`, executes Start/Stop/Restart/Delete
- **ProcessMonitorWorker**: Polls process health every 10s, publishes status events
- **Message Flow**: Command handling, response publishing, event broadcasting
- **Configuration**: Redis connection, ProcessInstanceManager options
- **Deployment**: Standalone service, Windows Service, systemd daemon

**Key Topics**: Command handling, health monitoring, message bus integration, process management

- **Error Handling**: HTTP status codes and error patterns
- **Extending the API**: Adding controllers, DTOs, middleware

**Key Topics**: REST endpoints, DTOs, controller patterns, JSON Patch, status polling endpoint

**API Highlights**:
- `POST /api/server/instances` - Create instance
- `PATCH /api/configuration/instances/{id}` - Update configuration (JSON Patch)
- `GET /api/status/recent-changes` - Poll status events (reconciliation)
- `POST /api/status/updates` - Receive daemon updates

---

### 🔄 Reconciliation Documentation

#### [DRaaS.Reconciliation README](src/DRaaS.Reconciliation/README.md)
**Reconciliation Worker Service Architecture**

Standalone worker service ensuring desired state convergence:
- **Architecture**: BackgroundService with periodic + event-driven reconciliation
- **Reconciliation Flow**: Detect drift → Apply strategy → Verify convergence
- **Reconciliation Strategies**: Restart, configuration update, platform migration
- **API-Driven Design**: All operations via ControlPlane HTTP API
- **Configuration**: Polling intervals, retry policies, enabled strategies
- **Status Polling**: Integration with `/api/status/recent-changes` endpoint

**Key Topics**: Drift detection, reconciliation strategies, API client, configuration state store, periodic reconciliation

**Key Features**:
- **Periodic Reconciliation**: Scheduled full system checks (configurable interval)
- **Event-Driven Reconciliation**: Immediate response to ConfigurationChanged events via API polling
- **Strategy Pattern**: Pluggable reconciliation strategies (Restart, Update, Migrate)
- **API-First**: 100% ControlPlane API driven (no direct Core dependencies)

---

### 📋 Example Configurations

#### [drasi-server YAML Example](src/DRaaS.ControlPlane/drasi-server-config-example.yaml)
**Generated Configuration Template**

Example of the YAML configuration that ProcessInstanceManager generates:
- **Server Settings**: id, host, port, logLevel, persistence
- **Sources**: PostgreSQL, mock, HTTP, gRPC examples
- **Queries**: Cypher queries with source subscriptions
- **Reactions**: Log, HTTP webhook, SSE streaming examples

This file demonstrates the complete structure and all available options for drasi-server configuration.

---

### 🗂️ Documentation Index

Quick reference to all documentation files:

| Document | Location | Purpose |
|----------|----------|---------|
| **Main README** | [README.md](README.md) | Project overview, architecture, getting started |
| **Core Library** | [DRaaS.Core/README.md](src/DRaaS.Core/README.md) | Business logic architecture, usage examples |
| **Service Layer** | [DRaaS.Core/Services/README.md](src/DRaaS.Core/Services/README.md) | Service organization and patterns |
| **Monitoring** | [DRaaS.Core/DISTRIBUTED_MONITORING.md](src/DRaaS.Core/DISTRIBUTED_MONITORING.md) | Status monitoring architecture |
| **Process Manager** | [DRaaS.Core/Providers/InstanceManagers/ProcessInstanceManager-README.md](src/DRaaS.Core/Providers/InstanceManagers/ProcessInstanceManager-README.md) | Process platform configuration |
| **ControlPlane API** | [DRaaS.ControlPlane/README.md](src/DRaaS.ControlPlane/README.md) | REST API documentation |
| **Reconciliation** | [DRaaS.Reconciliation/README.md](src/DRaaS.Reconciliation/README.md) | Reconciliation worker service |
| **YAML Example** | [drasi-server-config-example.yaml](src/DRaaS.ControlPlane/drasi-server-config-example.yaml) | Generated configuration template |

---

### 📚 Documentation Navigation Guide

**New to DRaaS?** Start here:
1. Read this README for overall architecture
2. Explore [DRaaS.Core README](src/DRaaS.Core/README.md) for business logic concepts
3. Check [DRaaS.ControlPlane README](src/DRaaS.ControlPlane/README.md) for API usage

**Implementing a platform manager?**
1. [DRaaS.Core README](src/DRaaS.Core/README.md) - Platform manager interface
2. [ProcessInstanceManager Configuration](src/DRaaS.Core/Providers/InstanceManagers/ProcessInstanceManager-README.md) - Reference implementation

**Setting up monitoring?**
1. [Distributed Monitoring](src/DRaaS.Core/DISTRIBUTED_MONITORING.md) - Complete monitoring guide
2. [Services Organization](src/DRaaS.Core/Services/README.md) - Monitoring service details

**Building a custom interface?**
1. [DRaaS.Core README](src/DRaaS.Core/README.md) - Library usage examples
2. [DRaaS.ControlPlane README](src/DRaaS.ControlPlane/README.md) - Reference implementation

**Understanding reconciliation?**
1. [DRaaS.Reconciliation README](src/DRaaS.Reconciliation/README.md) - Reconciliation architecture
2. [Distributed Monitoring](src/DRaaS.Core/DISTRIBUTED_MONITORING.md) - Status polling integration

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

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [Drasi](https://drasi.io/) - The continuous intelligence platform we're orchestrating
- Domain-Driven Design patterns
