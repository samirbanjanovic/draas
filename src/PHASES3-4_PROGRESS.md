# Phases 3 & 4 Complete - Docker and AKS Workers

## ✅ Phase 3 Complete: Docker Platform Worker

### Files Created

**DRaaS.Workers.Platform.Docker/**
- `DockerInstanceManager.cs` - Moved from Core (stub implementation)
- `DockerCommandWorker.cs` - Background service handling Docker commands (Start/Stop/Delete)
- `DockerMonitorWorker.cs` - Background service monitoring Docker container health
- `Program.cs` - DI configuration with Redis, message bus, workers
- `appsettings.json` - Redis connection configuration

### Build Status
✅ Docker worker project compiles successfully

### ServerController Updated
- `StartInstance` now routes Docker commands to `instance.commands.docker` channel
- `StopInstance` now routes Docker commands to `instance.commands.docker` channel
- Request/response pattern with 30s timeout
- Falls back to AKS manager for AKS platform (Phase 4)

## ⏳ Phase 4 In Progress: AKS Platform Worker

### Files Created So Far
**DRaaS.Workers.Platform.AKS/**
- `AksInstanceManager.cs` - Moved from Core (stub implementation)
- Project created with Core reference and StackExchange.Redis package

### Remaining Tasks
1. Create `AksCommandWorker.cs` - Following Docker/Process pattern
2. Create `AksMonitorWorker.cs` - Kubernetes pod monitoring
3. Configure `Program.cs` - DI setup
4. Create `appsettings.json` - Redis connection
5. Update ServerController to route AKS commands to message bus
6. Remove DockerInstanceManager and AksInstanceManager from Core
7. Remove instance manager registrations from ControlPlane Program.cs

## Architecture Summary

### Current State (After Phase 3)

```
API (ControlPlane)
  ├─ Process commands → Redis → DRaaS.Workers.Platform.Process
  ├─ Docker commands  → Redis → DRaaS.Workers.Platform.Docker
  └─ AKS commands     → Direct (AksInstanceManager in-process) ← Phase 4 will fix
  
Redis Message Bus
  ├─ instance.commands.process
  ├─ instance.commands.docker
  └─ instance.commands.aks (ready but no worker yet)
  
Workers (Independent Processes)
  ├─ DRaaS.Workers.Platform.Process
  │   ├─ ProcessInstanceManager
  │   ├─ ProcessCommandWorker (BackgroundService)
  │   └─ ProcessMonitorWorker (BackgroundService)
  │
  ├─ DRaaS.Workers.Platform.Docker
  │   ├─ DockerInstanceManager
  │   ├─ DockerCommandWorker (BackgroundService)
  │   └─ DockerMonitorWorker (BackgroundService)
  │
  └─ DRaaS.Workers.Platform.AKS (Phase 4)
      ├─ AksInstanceManager (created)
      ├─ AksCommandWorker (TODO)
      └─ AksMonitorWorker (TODO)
```

### Message Flow

**Docker Platform Example:**
1. Client → `POST /api/server/instances/{id}/start`
2. ServerController → Creates `StartInstanceCommand`
3. ServerController → `RequestAsync` to `instance.commands.docker` (30s timeout)
4. Redis Pub/Sub → Delivers wrapped message to DockerCommandWorker
5. DockerCommandWorker → Executes `DockerInstanceManager.StartInstanceAsync()`
6. DockerCommandWorker → Creates `StartInstanceResponse` with success/error
7. DockerCommandWorker → Publishes response to reply channel
8. Redis → Delivers response to waiting API
9. ServerController → Returns HTTP 200 with runtime info

## Pattern Established

All three workers follow the same structure:

### 1. Instance Manager
- Implements `IDrasiServerInstanceManager`
- Handles platform-specific operations (start, stop, restart, status)
- Manages runtime state via `IInstanceRuntimeStore`

### 2. Command Worker (BackgroundService)
- Subscribes to platform-specific command channel
- Handles Start/Stop/Restart/Delete commands
- Creates response objects
- Publishes responses to reply channel (request/response)
- Publishes events to `instance.events` channel (fire-and-forget)

### 3. Monitor Worker (BackgroundService)
- Polls instance health every 10-15 seconds
- Detects unexpected failures
- Publishes status change events
- Updates runtime store

### 4. Program.cs
- Redis connection from appsettings
- Message bus registration
- Instance manager registration
- Runtime store (InMemoryInstanceRuntimeStore)
- Both workers registered as hosted services

### 5. appsettings.json
```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

## Quick Reference: Completing AKS Worker

Based on the established pattern, here's what needs to be done:

### 1. AksCommandWorker.cs
Copy `DockerCommandWorker.cs` and replace:
- Namespace: `DRaaS.Workers.Platform.AKS`
- Class name: `AksCommandWorker`
- Logger: `ILogger<AksCommandWorker>`
- Channel: `Channels.GetInstanceCommandChannel(PlatformType.AKS)`
- Log messages: "AKS" instead of "Docker"

### 2. AksMonitorWorker.cs
Copy `DockerMonitorWorker.cs` and replace:
- Namespace: `DRaaS.Workers.Platform.AKS`
- Class name: `AksMonitorWorker`
- Logger: `ILogger<AksMonitorWorker>`
- Platform: `PlatformType.AKS`
- Log messages: "AKS" / "Kubernetes pod" instead of "Docker"

### 3. Program.cs
Copy `Docker/Program.cs` and replace:
- Namespace: `DRaaS.Workers.Platform.AKS`
- `AksInstanceManager` instead of `DockerInstanceManager`
- `AksCommandWorker` instead of `DockerCommandWorker`
- `AksMonitorWorker` instead of `DockerMonitorWorker`

### 4. appsettings.json
Same as Docker worker:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information",
      "DRaaS.Workers.Platform.AKS": "Information"
    }
  },
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

### 5. ServerController.cs
Update Start/Stop methods to add AKS routing:
```csharp
else if (instance.PlatformType == PlatformType.AKS)
{
    var command = new StartInstanceCommand { ... };
    var response = await _messageBus.RequestAsync<StartInstanceCommand, StartInstanceResponse>(
        Channels.GetInstanceCommandChannel(PlatformType.AKS),
        command,
        timeout: TimeSpan.FromSeconds(30));
    // Handle response...
}
```

### 6. Remove from Core
- Delete `DRaaS.Core/Providers/InstanceManagers/DockerInstanceManager.cs`
- Delete `DRaaS.Core/Providers/InstanceManagers/AksInstanceManager.cs`

### 7. Update ControlPlane Program.cs
Remove:
```csharp
builder.Services.AddSingleton<IDrasiServerInstanceManager, DockerInstanceManager>();
builder.Services.AddSingleton<IDrasiServerInstanceManager, AksInstanceManager>();
```

Comment:
```csharp
// Docker and AKS managers moved to DRaaS.Workers.Platform.Docker and DRaaS.Workers.Platform.AKS
// builder.Services.AddSingleton<IDrasiServerInstanceManager, DockerInstanceManager>();
// builder.Services.AddSingleton<IDrasiServerInstanceManager, AksInstanceManager>();
```

## Benefits Achieved

### 1. Fault Isolation
- Worker crash doesn't take down API
- Platform-specific failures isolated to that worker
- Can restart individual platform workers without affecting others

### 2. Independent Scaling
- Scale Process workers separately from Docker workers
- Different machines for different platforms
- Resource allocation per platform

### 3. Configuration-Driven Deployment
**Single Host (Development)**:
```json
// All components point to localhost:6379
"ConnectionStrings": { "Redis": "localhost:6379" }
```

**Distributed (Production)**:
```json
// API Server A
"ConnectionStrings": { "Redis": "redis-cluster.prod:6379" }

// Process Worker on Server B
"ConnectionStrings": { "Redis": "redis-cluster.prod:6379" }

// Docker Worker on Server C (with Docker daemon)
"ConnectionStrings": { "Redis": "redis-cluster.prod:6379" }

// AKS Worker on Server D (with kubectl configured)
"ConnectionStrings": { "Redis": "redis-cluster.prod:6379" }
```

No code changes required!

### 4. True Platform Decoupling
- API doesn't know about platform implementations
- Workers don't know about API existence
- Add new platforms by creating new worker projects
- Remove platforms by stopping workers

## Deployment Scenarios

### Scenario 1: Single Developer Machine
```bash
# Terminal 1
docker run -p 6379:6379 redis

# Terminal 2
cd DRaaS.ControlPlane
dotnet run

# Terminal 3
cd DRaaS.Workers.Platform.Process
dotnet run

# Terminal 4
cd DRaaS.Workers.Platform.Docker
dotnet run

# Terminal 5 (optional)
cd DRaaS.Workers.Platform.AKS
dotnet run
```

### Scenario 2: Multi-Host Production
```
Server A (API Host)
  └─ DRaaS.ControlPlane
      └─ Redis: redis-cluster.prod:6379

Server B (Process Platform Host)
  └─ DRaaS.Workers.Platform.Process
      └─ Redis: redis-cluster.prod:6379
      └─ drasi-server executable

Server C (Docker Platform Host)
  └─ DRaaS.Workers.Platform.Docker
      └─ Redis: redis-cluster.prod:6379
      └─ Docker daemon

Server D (AKS Platform Host)
  └─ DRaaS.Workers.Platform.AKS
      └─ Redis: redis-cluster.prod:6379
      └─ kubectl configured

Redis Cluster
  └─ Centralized message broker + state store
```

### Scenario 3: Kubernetes Deployment
```yaml
# API Deployment
apiVersion: apps/v1
kind: Deployment
metadata:
  name: draas-api
spec:
  replicas: 3
  template:
    spec:
      containers:
      - name: api
        image: draas/controlplane:latest
        env:
        - name: ConnectionStrings__Redis
          value: "redis-service:6379"

---
# Process Worker Deployment
apiVersion: apps/v1
kind: Deployment
metadata:
  name: draas-worker-process
spec:
  replicas: 2
  template:
    spec:
      containers:
      - name: worker
        image: draas/worker-process:latest
        env:
        - name: ConnectionStrings__Redis
          value: "redis-service:6379"

# Similar deployments for Docker and AKS workers
```

## Testing Checklist

After completing AKS worker:

- [ ] Build all projects successfully
- [ ] Start Redis locally
- [ ] Start all three workers
- [ ] Start API
- [ ] Test Process instance lifecycle (create, start, stop, delete)
- [ ] Test Docker instance lifecycle
- [ ] Test AKS instance lifecycle
- [ ] Verify events published to Redis
- [ ] Test timeout scenarios (stop worker, send command)
- [ ] Test error handling (invalid configuration)
- [ ] Test monitor workers detect failures
- [ ] Test distributed deployment (workers on different terminals/machines)

## Next Steps (Phase 5)

1. Complete AKS worker files (copy pattern from Docker)
2. Update ServerController for AKS routing
3. Remove Docker and AKS managers from Core
4. Remove manager registrations from ControlPlane
5. Update IInstanceManagerFactory (may no longer be needed)
6. Consider Redis-based state store instead of InMemoryInstanceRuntimeStore
7. Documentation updates
8. Performance testing
9. Production deployment guides
10. Monitoring and observability setup

## Files Modified

### Created (Phase 3 - Docker)
- `DRaaS.Workers.Platform.Docker/DockerInstanceManager.cs`
- `DRaaS.Workers.Platform.Docker/DockerCommandWorker.cs`
- `DRaaS.Workers.Platform.Docker/DockerMonitorWorker.cs`
- `DRaaS.Workers.Platform.Docker/Program.cs`
- `DRaaS.Workers.Platform.Docker/appsettings.json`

### Created (Phase 4 - AKS, Partial)
- `DRaaS.Workers.Platform.AKS/AksInstanceManager.cs`
- Project structure (csproj, references, packages)

### Modified
- `DRaaS.ControlPlane/Controllers/ServerController.cs` - Added Docker routing, prepared for AKS

## Build Status

✅ DRaaS.Core - Success  
✅ DRaaS.ControlPlane - Success  
✅ DRaaS.Workers.Platform.Process - Success  
✅ DRaaS.Workers.Platform.Docker - Success  
⏳ DRaaS.Workers.Platform.AKS - Partial (needs command/monitor workers)
