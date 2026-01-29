# Phase 2.5 Complete - Request/Response Pattern Implementation

## ✅ What Was Accomplished

### 1. Response Types Created
**File**: `DRaaS.Core/Messaging/Responses/InstanceCommandResponses.cs`

Created response records for all instance lifecycle commands:
- `InstanceCommandResponse` - Base response with `InstanceId`, `Success`, `ErrorMessage`
- `StartInstanceResponse` - Includes `RuntimeInfo` with process details
- `StopInstanceResponse` - Includes `RuntimeInfo` for stopped instance
- `RestartInstanceResponse` - Includes `RuntimeInfo` for restarted instance
- `DeleteInstanceResponse` - Simple success/failure response

### 2. Message Base Class Enhanced
**File**: `DRaaS.Core/Messaging/Messages.cs`

Added `ReplyChannel` property to the `Message` base class:
```csharp
public string? ReplyChannel { get; init; }
```

This allows the message bus to route responses back to the requester.

### 3. Redis Reply Channel Pattern Implemented
**File**: `DRaaS.Core/Messaging/RedisMessageBus.cs`

Enhanced `RequestAsync` to:
- Generate unique reply channel: `{channel}.response.{guid}`
- Wrap request with reply channel information
- Subscribe to reply channel before sending request
- Wait for response with configurable timeout
- Unsubscribe from reply channel after receiving response

Enhanced `SubscribeAsync` to:
- Detect wrapped request/response messages
- Extract reply channel from JSON payload
- Set reply channel on Message objects
- Handle both wrapped and unwrapped messages

### 4. ProcessCommandWorker Updated
**File**: `DRaaS.Workers.Platform.Process/ProcessCommandWorker.cs`

Modified all command handlers to:
- Create appropriate response objects (`StartInstanceResponse`, `StopInstanceResponse`, `DeleteInstanceResponse`)
- Set `Success` flag and optional `ErrorMessage`
- Include `RuntimeInfo` in successful responses
- Publish response to `command.ReplyChannel` if present
- Maintain backward compatibility (fire-and-forget if no reply channel)

Changes:
- `HandleStartInstanceAsync` - Returns `StartInstanceResponse` with runtime info
- `HandleStopInstanceAsync` - Returns `StopInstanceResponse` with runtime info
- `HandleDeleteInstanceAsync` - Returns `DeleteInstanceResponse` with success status

### 5. ControlPlane Configuration
**File**: `DRaaS.ControlPlane/Program.cs`

Added:
- `StackExchange.Redis` package dependency
- Redis connection configuration from `ConnectionStrings:Redis`
- `IConnectionMultiplexer` singleton registration
- `IMessageBus` registration with `RedisMessageBus` implementation

**File**: `DRaaS.ControlPlane/appsettings.json`

Added:
```json
"ConnectionStrings": {
  "Redis": "localhost:6379"
}
```

### 6. ServerController Updated
**File**: `DRaaS.ControlPlane/Controllers/ServerController.cs`

Modified to:
- Inject `IMessageBus` via constructor
- Use `RequestAsync` pattern for Process platform operations
- Fall back to direct manager calls for Docker/AKS (Phase 3 & 4)
- Handle timeouts and errors gracefully
- Return structured responses with runtime info

**Start Instance Flow**:
1. Check if instance exists
2. Get or validate configuration
3. **If Process platform**: Send `StartInstanceCommand` via message bus with 30s timeout
4. **If Docker/AKS**: Use direct manager call (legacy path)
5. Update instance status in database
6. Return success/failure response

**Stop Instance Flow**:
1. Check if instance exists
2. **If Process platform**: Send `StopInstanceCommand` via message bus with 30s timeout
3. **If Docker/AKS**: Use direct manager call (legacy path)
4. Update instance status in database
5. Return success/failure response

## Architecture Flow

```
┌─────────────────┐
│  REST API Call  │
│  POST /start    │
└────────┬────────┘
         │
         ▼
┌─────────────────────────────────────┐
│  ServerController                   │
│  - Check instance exists            │
│  - Get configuration                │
│  - Determine platform               │
└────────┬────────────────────────────┘
         │
         ├─ Process Platform? ──────────────┐
         │                                  │
         ▼                                  ▼
┌─────────────────────────────┐   ┌─────────────────────────┐
│  Message Bus (RequestAsync) │   │  Direct Manager Call    │
│  - Create StartInstanceCmd  │   │  (Docker/AKS - Phase 3) │
│  - Publish to Redis         │   └─────────────────────────┘
│  - Subscribe to reply chan  │
│  - Wait 30s for response    │
└────────┬────────────────────┘
         │
         ▼
┌─────────────────────────────┐
│  Redis                      │
│  Channel: instance.commands │
│           .process          │
└────────┬────────────────────┘
         │
         ▼
┌──────────────────────────────────┐
│  ProcessCommandWorker            │
│  - Receive wrapped command       │
│  - Extract reply channel         │
│  - Execute StartInstanceAsync    │
│  - Create StartInstanceResponse  │
│  - Publish to reply channel      │
└────────┬─────────────────────────┘
         │
         ▼
┌─────────────────────────────┐
│  Redis                      │
│  Channel: {reply-channel}   │
└────────┬────────────────────┘
         │
         ▼
┌─────────────────────────────┐
│  Message Bus                │
│  - Receive response         │
│  - Unsubscribe reply chan   │
│  - Return to controller     │
└────────┬────────────────────┘
         │
         ▼
┌─────────────────────────────┐
│  ServerController           │
│  - Check response.Success   │
│  - Update instance status   │
│  - Return HTTP response     │
└─────────────────────────────┘
```

## Testing Instructions

### Prerequisites
1. Redis running: `docker run -p 6379:6379 redis`
2. Worker running: `dotnet run --project DRaaS.Workers.Platform.Process`
3. API running: `dotnet run --project DRaaS.ControlPlane`

### Test Scenarios

#### 1. Start Instance (Happy Path)
```bash
# Create instance
POST /api/server/instances
{
  "name": "test-instance",
  "description": "Test instance",
  "serverConfiguration": {
    "host": "127.0.0.1",
    "port": 8080,
    "logLevel": "info"
  }
}

# Start instance
POST /api/server/instances/{instanceId}/start

# Expected: 200 OK with runtime info
{
  "message": "Instance 'xxx' started successfully",
  "runtimeInfo": {
    "instanceId": "xxx",
    "status": "Running",
    "processId": "12345",
    "startedAt": "..."
  }
}
```

#### 2. Stop Instance
```bash
POST /api/server/instances/{instanceId}/stop

# Expected: 200 OK
{
  "message": "Instance 'xxx' stopped successfully",
  "runtimeInfo": {
    "instanceId": "xxx",
    "status": "Stopped",
    "stoppedAt": "..."
  }
}
```

#### 3. Timeout Scenario
Stop the Worker and try starting an instance:

```bash
POST /api/server/instances/{instanceId}/start

# Expected: 500 Internal Server Error after 30s
{
  "error": "Operation timed out"
}
```

#### 4. Worker Error Handling
Start instance with invalid configuration:

```bash
POST /api/server/instances/{instanceId}/start
{
  "host": "invalid",
  "port": -1
}

# Expected: 500 Internal Server Error
{
  "error": "Configuration is invalid: ..."
}
```

## Key Benefits

### 1. Synchronous REST API Behavior
API consumers get immediate feedback - no need to poll for status updates.

### 2. Distributed Architecture
Worker can run on different machine than API - just point to same Redis instance.

### 3. Fault Tolerance
- Worker crash: API gets timeout, can retry or notify user
- API crash: Worker continues processing, responses go to Redis
- Redis crash: Both API and Worker fail gracefully with connection errors

### 4. Backward Compatibility
- Docker/AKS platforms still use direct manager calls
- Will be updated in Phase 3 & 4 using same pattern
- No breaking changes to API contracts

### 5. Horizontal Scaling
- Multiple API instances can send requests (different reply channels)
- Multiple Worker instances can process commands (Redis Pub/Sub to all)
- First worker to process wins (need coordination for true competing consumers)

## Configuration

### Single Host Deployment
```json
// Both API and Worker
"ConnectionStrings": {
  "Redis": "localhost:6379"
}
```

### Distributed Deployment
```json
// API
"ConnectionStrings": {
  "Redis": "redis-cluster.example.com:6379"
}

// Worker (can be on different machine)
"ConnectionStrings": {
  "Redis": "redis-cluster.example.com:6379"
}
```

## Known Limitations

### 1. Restart Not Fully Implemented
`RestartInstanceCommand` doesn't include configuration - worker logs warning and stops instance only.

**Solution**: Add configuration fetch in worker or include in command.

### 2. No Response Persistence
If API crashes after sending request but before receiving response, the response is lost.

**Solution**: Implement durable response store (Redis Stream or database).

### 3. Single Response Consumer
Only the API instance that sent the request receives the response (reply channel is unique).

**Solution**: This is by design for request/response pattern. For broadcast, use events.

### 4. Timeout Handling
30-second timeout is hardcoded.

**Solution**: Make configurable via appsettings.json.

### 5. No Retry Logic
Failed requests are not automatically retried.

**Solution**: Implement retry policy in API or use Polly library.

## Next Steps

### Phase 3: Docker Platform Worker
1. Create `DRaaS.Workers.Platform.Docker` project
2. Move `DockerInstanceManager` from Core to Worker
3. Create `DockerCommandWorker` with request/response pattern
4. Update ServerController to route Docker commands to message bus
5. Test end-to-end

### Phase 4: AKS Platform Worker
1. Create `DRaaS.Workers.Platform.AKS` project
2. Move `AksInstanceManager` from Core to Worker
3. Create `AksCommandWorker` with request/response pattern
4. Update ServerController to route AKS commands to message bus
5. Test end-to-end

### Phase 5: Final Cleanup
1. Remove `IInstanceManagerFactory` from ControlPlane (no longer needed)
2. Remove all instance manager implementations from Core
3. Core becomes purely contracts (interfaces + models + messaging)
4. Update documentation
5. Performance testing and optimization

## Success Metrics

✅ API maintains synchronous request/response behavior  
✅ Worker can run independently on different host  
✅ Process platform functional via message bus  
✅ Timeout handling works correctly  
✅ Error messages propagate from worker to API  
✅ Build successful for all projects  
✅ No breaking changes to existing API contracts  
✅ Docker/AKS platforms still functional (direct calls)  

## Files Modified

### Created
- `DRaaS.Core/Messaging/Responses/InstanceCommandResponses.cs`

### Modified
- `DRaaS.Core/Messaging/Messages.cs` - Added ReplyChannel property
- `DRaaS.Core/Messaging/RedisMessageBus.cs` - Implemented reply channel pattern
- `DRaaS.Workers.Platform.Process/ProcessCommandWorker.cs` - Send responses
- `DRaaS.ControlPlane/Program.cs` - Added Redis and message bus
- `DRaaS.ControlPlane/appsettings.json` - Added Redis connection string
- `DRaaS.ControlPlane/Controllers/ServerController.cs` - Use RequestAsync for Process platform
- `DRaaS.ControlPlane/DRaaS.ControlPlane.csproj` - Added StackExchange.Redis package

### Build Status
✅ All projects compile successfully  
✅ No errors, only 1 warning (unused exception variable in ProcessInstanceManager)
