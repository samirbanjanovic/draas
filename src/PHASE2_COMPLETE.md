# DRaaS Core Cleanup - Phase 2 Complete

## What Was Done

### Removed from DRaaS.Core
1. **ProcessInstanceManager.cs** - Moved to `DRaaS.Workers.Platform.Process`
2. **ProcessInstanceManagerOptions.cs** - Moved to `DRaaS.Workers.Platform.Process`
3. **ProcessInstanceManager-README.md** - Documentation moved to Worker project

### Updated in DRaaS.ControlPlane
- **Program.cs** - Removed ProcessInstanceManager registration and ProcessStatusMonitor setup
- Process platform now handled entirely by the Worker service
- Docker and AKS managers remain in Core (will be moved in Phase 3 & 4)

### What Remains in Core
✅ **Interfaces (Contracts)**:
- `IDrasiServerInstanceManager` - Contract for all instance managers
- `IMessageBus` - Message bus abstraction
- `IStatusMonitor` - Status monitoring contract
- All service interfaces

✅ **Models**:
- `DrasiInstance`, `Configuration`, `InstanceStatus`, etc.
- All domain models remain in Core

✅ **Messaging Contracts**:
- Commands: `StartInstanceCommand`, `StopInstanceCommand`, etc.
- Events: `InstanceStartedEvent`, `InstanceStatusChangedEvent`, etc.
- `Channels` - Channel routing helpers

✅ **Implementations Still in Core (Temporary)**:
- `DockerInstanceManager` - Will move to `DRaaS.Workers.Platform.Docker` (Phase 3)
- `AksInstanceManager` - Will move to `DRaaS.Workers.Platform.AKS` (Phase 4)
- `ProcessStatusMonitor` - Will be removed after all workers have monitors

## Current Architecture State

### Process Platform (Distributed) ✅
```
ControlPlane API
  ↓ (No direct access to Process manager)
  ↓ Will publish commands to Redis (Phase 2.5)
Redis Message Bus
  ↓ instance.commands.process
DRaaS.Workers.Platform.Process
  ↓ ProcessCommandWorker handles commands
  ↓ ProcessMonitorWorker monitors health
drasi-server processes
```

### Docker & AKS Platforms (Still Monolithic) ⏳
```
ControlPlane API
  ↓ Direct service calls
DockerInstanceManager (in-process)
  ↓ Docker API calls
Docker containers

ControlPlane API
  ↓ Direct service calls
AksInstanceManager (in-process)
  ↓ Kubernetes API calls
AKS pods
```

## Critical Issue: API Sync/Async Challenge

### The Problem
With ProcessInstanceManager moved to a worker, the API can no longer make direct synchronous calls:

**Before (Synchronous)**:
```csharp
[HttpPost("instances/{instanceId}/start")]
public async Task<IActionResult> StartInstance(string instanceId, Configuration config)
{
    var result = await _instanceManager.StartInstanceAsync(instanceId, config);
    return Ok(result); // Immediate response with actual result
}
```

**After (Async/Fire-and-Forget)**:
```csharp
[HttpPost("instances/{instanceId}/start")]
public async Task<IActionResult> StartInstance(string instanceId, Configuration config)
{
    await _messageBus.PublishAsync(channel, new StartInstanceCommand { ... });
    return Ok("Command published"); // But did it succeed? We don't know!
}
```

### Proposed Solutions

#### Option 1: Request/Response Pattern (Recommended)
Use `IMessageBus.RequestAsync<TRequest, TResponse>` for synchronous behavior over message bus.

**Changes Needed**:
1. Create response types:
   ```csharp
   public record StartInstanceResponse : QueryResponse
   {
       public bool Success { get; init; }
       public string? ErrorMessage { get; init; }
       public InstanceRuntimeInfo? RuntimeInfo { get; init; }
   }
   ```

2. Implement request/response in RedisMessageBus (reply channel pattern):
   - Publisher sends request with unique reply channel
   - Subscriber handles request and publishes response to reply channel
   - Publisher waits on reply channel with timeout

3. Update ProcessCommandWorker to handle requests and send responses

4. Update ServerController to use RequestAsync:
   ```csharp
   var response = await _messageBus.RequestAsync<StartInstanceCommand, StartInstanceResponse>(
       channel, command, timeout: TimeSpan.FromSeconds(30));
   ```

**Pros**: Maintains REST API semantics, immediate feedback, uses existing infrastructure  
**Cons**: Additional complexity in message bus, timeout management required

#### Option 2: 202 Accepted Pattern
API returns immediately with operation ID, client polls for status.

```csharp
[HttpPost("instances/{instanceId}/start")]
public async Task<IActionResult> StartInstance(string instanceId)
{
    var operationId = Guid.NewGuid().ToString();
    await _messageBus.PublishAsync(channel, new StartInstanceCommand 
    { 
        CorrelationId = operationId 
    });
    return Accepted(new { operationId, statusUrl = $"/operations/{operationId}" });
}

[HttpGet("operations/{operationId}")]
public async Task<IActionResult> GetOperationStatus(string operationId)
{
    // Check operation status from event store or state
}
```

**Pros**: Standard pattern for long-running operations, fully async  
**Cons**: Requires client changes, multiple API calls, operation tracking infrastructure

#### Option 3: Hybrid Approach
- **Lifecycle commands** (Start/Stop/Delete) → Request/Response for immediate feedback
- **Status queries** (GetInstance) → Read from shared state store (Redis/DB)
- **Background operations** (Reconciliation, Monitoring) → Fire-and-forget events

**Pros**: Best of both worlds, minimal client impact  
**Cons**: Two communication patterns to maintain

## Recommended Next Steps

### Phase 2.5: Implement Request/Response Pattern
1. ✅ Create response types for all commands
2. ✅ Implement Redis reply channel pattern in RedisMessageBus
3. ✅ Update ProcessCommandWorker to send responses
4. ✅ Update ServerController to use RequestAsync
5. ✅ Test end-to-end with Worker running

### Phase 3: Docker Platform Worker
1. Create `DRaaS.Workers.Platform.Docker` project
2. Move `DockerInstanceManager` from Core to Worker
3. Create `DockerCommandWorker` and `DockerMonitorWorker`
4. Remove `DockerInstanceManager` from Core
5. Update API to route Docker commands to message bus

### Phase 4: AKS Platform Worker
1. Create `DRaaS.Workers.Platform.AKS` project
2. Move `AksInstanceManager` from Core to Worker
3. Create `AksCommandWorker` and `AksMonitorWorker`
4. Remove `AksInstanceManager` from Core
5. Update API to route AKS commands to message bus

### Phase 5: Final Cleanup
1. Remove `ProcessStatusMonitor` from Core (all monitors in workers)
2. Remove `IStatusMonitor` interface (no longer needed in Core)
3. Update documentation to reflect fully distributed architecture
4. Remove in-process orchestration code
5. Core becomes **purely contracts** (interfaces + models only)

## Current Status

- ✅ **Phase 1**: Messaging infrastructure complete
- ✅ **Phase 2**: Process worker created and Core cleaned up
- ⏳ **Phase 2.5**: Request/Response pattern needed
- ⏳ **Phase 3**: Docker worker (pending)
- ⏳ **Phase 4**: AKS worker (pending)
- ⏳ **Phase 5**: Final cleanup (pending)

## Breaking Changes

### For ControlPlane API
- Process platform operations currently **not functional** (no ProcessInstanceManager registered)
- Docker and AKS platforms still work (in-process managers)
- **Action Required**: Implement Phase 2.5 before Process platform can be used

### For Clients
- Once Phase 2.5 complete: No changes needed (same REST API)
- If using 202 Accepted pattern: Client must poll operation status

### For Deployment
- Process platform requires `DRaaS.Workers.Platform.Process` running
- Redis must be accessible from both API and Worker
- Configuration change only for distributed deployment (Redis connection string)

## Testing Strategy

### Phase 2.5 Validation
1. Start Redis: `docker run -p 6379:6379 redis`
2. Start Worker: `dotnet run --project DRaaS.Workers.Platform.Process`
3. Start API: `dotnet run --project DRaaS.ControlPlane`
4. Test command: `POST /api/server/instances/{id}/start`
5. Verify: Worker handles command, API receives response, instance starts

### Integration Test Checklist
- [ ] Worker receives commands from Redis
- [ ] Worker executes ProcessInstanceManager operations
- [ ] Worker publishes events to Redis
- [ ] API receives responses from Worker
- [ ] API returns correct HTTP status codes
- [ ] Timeout handling works correctly
- [ ] Error messages propagate correctly
- [ ] Monitor detects process failures
- [ ] Status events published and consumed

## Open Questions

1. **Should we implement Phase 2.5 now, or continue with Phase 3 & 4 first?**
   - Implement now: Process platform functional, pattern proven for others
   - Continue later: Docker/AKS move faster, but Process platform unusable

2. **Should API support both direct calls (for in-process managers) and message bus (for workers)?**
   - Hybrid: Transition period smoother, but more code to maintain
   - Pure message bus: Cleaner architecture, but requires Phase 2.5 first

3. **How to handle shared state across workers?**
   - Current: InMemoryInstanceRuntimeStore (not shared)
   - Needed: Redis-based state store for distributed scenarios

4. **Should we remove IInstanceManagerFactory from Core?**
   - Current: Used by API to route to platform-specific managers
   - Future: Message bus routing replaces factory pattern
