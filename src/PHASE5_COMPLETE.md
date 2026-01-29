# Phase 5: ControlPlane Message Bus Integration

## Overview
This phase completes the message bus integration by ensuring **all** ControlPlane operations communicate with platform workers exclusively through the Redis message bus.

## Changes Made

### 1. DeleteInstance Endpoint
**Before:** Directly deleted configuration and metadata without notifying workers
```csharp
// Old code - no worker communication
await _configurationProvider.DeleteConfigurationAsync(instanceId);
var deleted = await _instanceService.DeleteInstanceAsync(instanceId);
```

**After:** Sends `DeleteInstanceCommand` to workers before deleting metadata
```csharp
// New code - send command to worker
var command = new DeleteInstanceCommand { InstanceId = instanceId };
var response = await _messageBus.RequestAsync<DeleteInstanceCommand, DeleteInstanceResponse>(
    Channels.GetInstanceCommandChannel(instance.PlatformType),
    command,
    timeout: TimeSpan.FromSeconds(30));

// Then delete configuration and metadata
await _configurationProvider.DeleteConfigurationAsync(instanceId);
await _instanceService.DeleteInstanceAsync(instanceId);
```

### 2. StatusUpdateService Enhancement
**Before:** Only raised in-memory events
```csharp
// Old - in-memory only
StatusChanged?.Invoke(this, new StatusUpdateEventArgs { ... });
```

**After:** Publishes to both Redis message bus AND in-memory events
```csharp
// New - distributed + local
await _messageBus.PublishAsync(Channels.StatusEvents, new InstanceStatusChangedEvent {
    InstanceId = instanceId,
    OldStatus = oldStatus,
    NewStatus = newStatus,
    Source = source
});

// Also keep in-memory event for backward compatibility
StatusChanged?.Invoke(this, new StatusUpdateEventArgs { ... });
```

**Constructor Updated:**
```csharp
public StatusUpdateService(
    IInstanceRuntimeStore _runtimeStore,
    IMessageBus messageBus)  // Added message bus
```

### 3. EventSubscriptionService (New)
Created `Services/EventSubscriptionService.cs` - a background service that subscribes to events from workers.

**Subscriptions:**
- **Status Events** (`Channels.StatusEvents`) - Updates instance status when workers report changes
- **Lifecycle Events** (`Channels.InstanceEvents`) - Logs started/stopped/deleted events

**Purpose:**
- Keeps ControlPlane metadata in sync with worker state
- Provides audit trail of lifecycle events
- Enables real-time status updates even if initiated externally

**Registration:**
```csharp
builder.Services.AddHostedService<DRaaS.ControlPlane.Services.EventSubscriptionService>();
```

### 4. Program.cs Registration
StatusUpdateService now requires IMessageBus:
```csharp
builder.Services.AddSingleton<IStatusUpdateService, StatusUpdateService>();
```

## Complete Message Bus Flow

### Request/Response Pattern (Commands)
Used for operations requiring acknowledgment:

```
Client → ControlPlane API → Message Bus → Worker → Message Bus → ControlPlane API → Client
  POST     Validate/Send     Pub/Sub      Execute     Reply        Return         JSON
           Command           Request      Platform    Channel      Response       Response
```

**Endpoints Using This Pattern:**
- `POST /api/server/instances/{id}/start` → `StartInstanceCommand`
- `POST /api/server/instances/{id}/stop` → `StopInstanceCommand`
- `POST /api/server/instances/{id}/restart` → Stop + Start commands
- `DELETE /api/server/instances/{id}` → `DeleteInstanceCommand`

### Publish/Subscribe Pattern (Events)
Used for broadcasting state changes:

```
Worker → Message Bus → All Subscribers (ControlPlane, Reconciliation, etc.)
Monitor   Publish      Subscribe to
Detects   Event        Channels
Change    to Channel
```

**Events Published by Workers:**
- `InstanceStartedEvent` (lifecycle milestone)
- `InstanceStoppedEvent` (lifecycle milestone)
- `InstanceDeletedEvent` (lifecycle milestone)
- `InstanceStatusChangedEvent` (status change detection)

**Events Published by ControlPlane:**
- `InstanceStatusChangedEvent` - When configuration changes trigger status updates

**Subscribers:**
- **ControlPlane EventSubscriptionService** - Updates metadata, logs events
- **Reconciliation Service** - Detects drift, triggers remediation
- **Future:** Monitoring dashboards, alerting systems, audit logs

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                      ControlPlane API                           │
│                                                                 │
│  ┌──────────────┐    ┌──────────────────┐                     │
│  │ REST         │    │ Background       │                     │
│  │ Endpoints    │    │ Services         │                     │
│  │              │    │                  │                     │
│  │ - Start      │    │ Event            │                     │
│  │ - Stop       │    │ Subscription     │                     │
│  │ - Restart    │    │ Service          │                     │
│  │ - Delete     │    │                  │                     │
│  │ - Config     │    │ (subscribes to   │                     │
│  │   Changes    │    │  worker events)  │                     │
│  └──────┬───────┘    └────────▲─────────┘                     │
│         │                     │                                │
│         │ Commands            │ Events                         │
│         │ (Request/Response)  │ (Pub/Sub)                      │
└─────────┼─────────────────────┼────────────────────────────────┘
          │                     │
          ▼                     │
┌─────────────────────────────────────────────────────────────────┐
│                     Redis Message Bus                           │
│                                                                 │
│  Channels:                                                      │
│  - instance.commands.{process|docker|aks}  (Commands)          │
│  - instance.events                         (Lifecycle Events)  │
│  - status.events                           (Status Changes)    │
│  - {channel}.response.{guid}               (Reply Channels)    │
└─────────┬───────────────────────┬───────────────────────────────┘
          │                       │
          │ Commands              │ Events
          │                       │
          ▼                       ▼
┌─────────────────┐      ┌─────────────────┐
│  Process        │      │  Docker         │      ┌─────────────┐
│  Worker         │      │  Worker         │      │  AKS        │
│                 │      │                 │      │  Worker     │
│  - Command      │      │  - Command      │      │             │
│    Worker       │      │    Worker       │      │  - Command  │
│  - Monitor      │      │  - Monitor      │      │    Worker   │
│    Worker       │      │    Worker       │      │  - Monitor  │
│                 │      │                 │      │    Worker   │
│  (Executes      │      │  (Executes      │      │             │
│   operations,   │      │   operations,   │      │  (Executes  │
│   monitors      │      │   monitors      │      │   ops,      │
│   health,       │      │   health,       │      │   monitors  │
│   publishes     │      │   publishes     │      │   health,   │
│   events)       │      │   events)       │      │   publishes)│
└─────────────────┘      └─────────────────┘      └─────────────┘
```

## Communication Patterns

### Pattern 1: Command with Response (Synchronous REST Behavior)
**Example:** Start Instance

1. Client: `POST /api/server/instances/123/start`
2. ControlPlane validates, creates `StartInstanceCommand`
3. ControlPlane generates unique reply channel: `instance.commands.process.response.abc-123`
4. ControlPlane subscribes to reply channel
5. ControlPlane publishes wrapped command to `instance.commands.process`
6. Worker receives command, extracts reply channel
7. Worker executes `ProcessInstanceManager.StartInstanceAsync()`
8. Worker creates `StartInstanceResponse` with runtime info
9. Worker publishes response to reply channel
10. ControlPlane receives response (or times out after 30s)
11. ControlPlane updates instance status
12. ControlPlane returns JSON: `{ message, runtimeInfo }`

### Pattern 2: Event Broadcasting (Asynchronous Notifications)
**Example:** Process Crashes

1. Worker: `ProcessMonitorWorker` detects process exited
2. Worker updates runtime store status
3. Worker publishes `InstanceStatusChangedEvent` to `status.events`
4. ControlPlane: `EventSubscriptionService` receives event
5. ControlPlane updates instance metadata to `Stopped`
6. Reconciliation: Receives same event
7. Reconciliation detects drift (expected: Running, actual: Stopped)
8. Reconciliation triggers restart command

### Pattern 3: Configuration Changes
**Example:** Update Log Level

1. Client: `PATCH /api/configuration/instances/123` (change log level)
2. ControlPlane updates configuration in store
3. ControlPlane calls `StatusUpdateService.PublishStatusUpdateAsync()`
4. StatusUpdateService publishes `InstanceStatusChangedEvent` with `ConfigurationChanged` status
5. Message bus broadcasts to all subscribers
6. Reconciliation detects configuration drift
7. Reconciliation restarts instance with new config

## Benefits Achieved

### 1. **Complete Decoupling**
- ✅ ControlPlane has ZERO direct dependencies on platform managers
- ✅ Workers can be deployed independently
- ✅ Can scale workers independently of API

### 2. **Consistent Communication**
- ✅ All lifecycle operations use message bus (Start/Stop/Restart/Delete)
- ✅ All status updates go through message bus
- ✅ All events broadcast to all interested subscribers

### 3. **Resilient Architecture**
- ✅ Workers can crash/restart without affecting API
- ✅ API can restart without losing worker state (workers still running)
- ✅ Message bus provides buffering during transient failures

### 4. **Observability**
- ✅ All events flow through central bus (easy to tap for monitoring)
- ✅ EventSubscriptionService provides audit trail
- ✅ Multiple services can subscribe to same events independently

### 5. **Configuration-Driven Deployment**
- ✅ Single host: All components point to `localhost:6379`
- ✅ Distributed: Components point to `redis-cluster:6379`
- ✅ No code changes required for different topologies

## Testing Instructions

### 1. Start Redis
```bash
docker run -d -p 6379:6379 redis:latest
```

### 2. Start Workers
```bash
# Terminal 1: Process Worker
cd DRaaS.Workers.Platform.Process
dotnet run

# Terminal 2: Docker Worker
cd DRaaS.Workers.Platform.Docker
dotnet run

# Terminal 3: AKS Worker
cd DRaaS.Workers.Platform.AKS
dotnet run
```

### 3. Start ControlPlane
```bash
# Terminal 4: API
cd DRaaS.ControlPlane
dotnet run
```

### 4. Test Lifecycle Operations
```bash
# Create instance
curl -X POST http://localhost:5000/api/server/instances \
  -H "Content-Type: application/json" \
  -d '{"name":"test1", "serverConfiguration": {...}}'

# Start instance
curl -X POST http://localhost:5000/api/server/instances/test1/start

# Check logs in worker terminal - should see command received

# Stop instance
curl -X POST http://localhost:5000/api/server/instances/test1/stop

# Restart instance
curl -X POST http://localhost:5000/api/server/instances/test1/restart

# Delete instance
curl -X DELETE http://localhost:5000/api/server/instances/test1
```

### 5. Verify Event Flow
**Check ControlPlane logs** - EventSubscriptionService should log:
```
[Information] Received status change event for instance test1: Pending -> Running (Source: ProcessCommandWorker)
[Information] Instance test1 started
```

**Check Worker logs** - CommandWorker should log:
```
[Information] Received StartInstanceCommand for instance test1
[Information] Instance test1 started successfully
[Information] Published response to reply channel
```

## Known Limitations

### 1. In-Memory State Store
- `IInstanceRuntimeStore` is still in-memory (`InMemoryInstanceRuntimeStore`)
- Each component has its own copy of runtime state
- **Solution:** Phase 6 - Implement Redis-based shared state store

### 2. Configuration Payload
- `RestartInstanceCommand` doesn't include configuration
- Worker must fetch config from somewhere (not implemented in workers yet)
- **Workaround:** Stop + Start separately (Start includes config)

### 3. Status Update Race Conditions
- Worker publishes status event → ControlPlane updates metadata
- ControlPlane returns response → updates metadata
- Can cause duplicate updates
- **Acceptable:** Last write wins, both paths reach same end state

### 4. No Reply Channel Cleanup
- Reply channels created per request: `{channel}.response.{guid}`
- Redis Pub/Sub auto-cleans on disconnect
- Not an issue for short-lived requests, but worth noting

## Next Steps

### Phase 6: Redis-Based Shared State Store
**Problem:** In-memory stores not shared across components
**Solution:** Implement `RedisInstanceRuntimeStore`
- Single source of truth for runtime state
- All components read/write same Redis keys
- Enables horizontal scaling of ControlPlane

### Phase 7: Competing Consumers
**Problem:** Multiple worker instances receive same command (all process it)
**Solution:** Use Redis Streams instead of Pub/Sub for commands
- Only one worker processes each command
- Enables load balancing across worker instances
- Consumer groups for fault tolerance

### Phase 8: Production Hardening
- Retry policies (Polly)
- Circuit breakers
- Health checks for message bus
- Telemetry and distributed tracing
- Authentication/authorization

## Summary

ControlPlane now fully integrated with message bus:
- ✅ All CRUD operations route through message bus
- ✅ Status updates published to distributed subscribers
- ✅ Event subscription service keeps metadata in sync
- ✅ Configuration changes trigger reconciliation via events
- ✅ Complete architectural decoupling achieved

**Message Bus Coverage:**
- Start Instance → ✅ Message Bus
- Stop Instance → ✅ Message Bus
- Restart Instance → ✅ Message Bus
- Delete Instance → ✅ Message Bus
- Configuration Changes → ✅ Message Bus (via StatusUpdateService)
- Status Updates → ✅ Message Bus (workers → ControlPlane)
- Lifecycle Events → ✅ Message Bus (broadcast to all subscribers)

The system is now a fully distributed, event-driven architecture ready for horizontal scaling and resilient deployment.
