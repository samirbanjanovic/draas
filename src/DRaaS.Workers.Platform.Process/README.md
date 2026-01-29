# DRaaS.Workers.Platform.Process

Worker service that manages Drasi server instances running as native OS processes on the host machine. This service uses a message bus architecture with Redis Pub/Sub to receive commands and publish events, enabling distributed coordination across multiple system components.

## Architecture

The Process Platform Worker consists of two background services that work together to manage process instances:

### ProcessCommandWorker (BackgroundService)
Subscribes to the platform-specific command channel and processes incoming commands.

**Command Flow**:
1. Subscribes to `instance.commands.process` channel via Redis message bus
2. Receives commands: StartInstanceCommand, StopInstanceCommand, RestartInstanceCommand, DeleteInstanceCommand
3. Executes operations using ProcessInstanceManager
4. Publishes responses with request/response pattern via unique reply channels
5. Publishes lifecycle events to `instance.events` channel

### ProcessMonitorWorker (BackgroundService)
Continuously monitors running process instances and reports status changes.

**Monitoring Flow**:
1. Polls running processes every 10 seconds
2. Detects unexpected process exits or crashes
3. Updates runtime state via IInstanceRuntimeStore
4. Publishes InstanceStatusChangedEvent to `instance.events` channel

### ProcessInstanceManager
Manages the lifecycle of drasi-server processes on the local machine.

**Responsibilities**:
- Launch drasi-server processes with generated YAML configurations
- Track running processes in concurrent dictionary (PID tracking)
- Monitor process health (HasExited, ExitCode detection)
- Handle graceful shutdown with configurable timeout
- Clean up process resources and configuration files


## Message Bus Architecture

The worker uses Redis Pub/Sub for decoupled communication with other system components. Communication follows two patterns: request/response for commands and broadcast for events.

### Channels

The `Channels` class in DRaaS.Core.Messaging defines all channel names and provides helper methods for platform-specific routing.

**Command Channels** (platform-specific, point-to-point):
- `instance.commands.process` - Commands for Process platform instances
- `instance.commands.docker` - Commands for Docker platform instances
- `instance.commands.aks` - Commands for AKS platform instances

**Event Channels** (broadcast, pub/sub):
- `instance.events` - Instance lifecycle events (started, stopped, failed)
- `configuration.events` - Configuration change notifications
- `status.events` - Status update events

**Channel Selection**:
```csharp
// Automatically route to correct channel based on platform type
var channel = Channels.GetInstanceCommandChannel(PlatformType.Process);
// Returns: "instance.commands.process"
```

### Request/Response Pattern

Commands use a temporary reply channel pattern for synchronous-style responses:

1. Caller publishes command with `ReplyChannel` property
2. Worker processes command
3. Worker publishes response to the unique `ReplyChannel`
4. Caller receives response and completes awaited task
5. Temporary channel is cleaned up

**Example Flow**:
```csharp
// Caller side (API or service)
var response = await messageBus.RequestAsync<StartInstanceCommand, StartInstanceResponse>(
    channel: "instance.commands.process",
    request: command,
    timeout: TimeSpan.FromSeconds(30)
);

// Worker side (ProcessCommandWorker)
// 1. Receives command with ReplyChannel property
// 2. Executes operation
// 3. Publishes response to ReplyChannel
await messageBus.PublishAsync(command.ReplyChannel, response);
```

### Command Messages

All commands inherit from the base `Message` class with correlation tracking.

```csharp
// Start instance command
public record StartInstanceCommand : Message
{
    public required string InstanceId { get; init; }
    public required Configuration Configuration { get; init; }
}

// Stop instance command
public record StopInstanceCommand : Message
{
    public required string InstanceId { get; init; }
}

// Restart instance command
public record RestartInstanceCommand : Message
{
    public required string InstanceId { get; init; }
    public Configuration? NewConfiguration { get; init; }
}

// Delete instance command
public record DeleteInstanceCommand : Message
{
    public required string InstanceId { get; init; }
}
```

### Response Messages

Workers publish responses back through reply channels for request/response operations.

```csharp
// Generic response structure
public record StartInstanceResponse : Message
{
    public required string InstanceId { get; init; }
    public required bool Success { get; init; }
    public InstanceRuntimeInfo? RuntimeInfo { get; init; }
    public string? ErrorMessage { get; init; }
    public string? CorrelationId { get; init; }
}
```

### Event Messages

The worker publishes broadcast events when operations complete or status changes occur.

```csharp
// Instance started successfully
public record InstanceStartedEvent : Message
{
    public required string InstanceId { get; init; }
    public required InstanceRuntimeInfo RuntimeInfo { get; init; }
}

// Instance stopped
public record InstanceStoppedEvent : Message
{
    public required string InstanceId { get; init; }
    public string? Reason { get; init; }
}

// Status changed (health monitoring)
public record InstanceStatusChangedEvent : Message
{
    public required string InstanceId { get; init; }
    public required InstanceStatus OldStatus { get; init; }
    public required InstanceStatus NewStatus { get; init; }
    public string? Source { get; init; }
    public string? Reason { get; init; }
}

// Operation failed
public record InstanceOperationFailedEvent : Message
{
    public required string InstanceId { get; init; }
    public required string Operation { get; init; }
    public required string Error { get; init; }
}
```

## Configuration

Configure the worker via `appsettings.json`:

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
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "DRaaS.Workers.Platform.Process": "Debug"
    }
  }
}
```

### Redis Connection

The worker requires a Redis instance for message bus communication.

**Single Host Deployment**:
```json
{ "ConnectionStrings": { "Redis": "localhost:6379" } }
```
All components (API, Workers, Redis) run on the same machine with minimal latency.

**Distributed Deployment**:
```json
{ "ConnectionStrings": { "Redis": "redis-cluster.example.com:6379" } }
```
Workers run on separate machines from the API, sharing a centralized Redis instance for message coordination.

**Redis Cluster** (High Availability):
```json
{ "ConnectionStrings": { "Redis": "redis-node1:6379,redis-node2:6379,redis-node3:6379" } }
```
Multi-node Redis cluster configuration for fault tolerance.

### ProcessInstanceManager Options
- **ExecutablePath**: Path to drasi-server executable (can be in PATH or absolute path)
- **InstanceConfigDirectory**: Directory where instance YAML configs are written
- **DefaultLogLevel**: Default log level for drasi-server instances (debug, info, warn, error)
- **ShutdownTimeoutSeconds**: Timeout for graceful shutdown before force kill
- **WorkingDirectory**: Working directory for drasi-server processes

## Running the Worker

### Development
```bash
cd src/DRaaS.Workers.Platform.Process
dotnet run
```

### Production
```bash
dotnet publish -c Release
./bin/Release/net10.0/DRaaS.Workers.Platform.Process
```

### As a Windows Service
```powershell
sc.exe create DRaaSProcessWorker binPath= "C:\path\to\DRaaS.Workers.Platform.Process.exe"
sc.exe start DRaaSProcessWorker
```

### As a Linux Systemd Service
```bash
# Create service file at /etc/systemd/system/draas-process-worker.service
[Unit]
Description=DRaaS Process Platform Worker
After=network.target

[Service]
Type=notify
ExecStart=/usr/local/bin/DRaaS.Workers.Platform.Process
Restart=always

[Install]
WantedBy=multi-user.target
```

## Scaling

### Single Host Deployment
All components run on one machine:
- Redis: localhost:6379
- Worker: Processes commands locally
- API: Publishes commands to same Redis instance
- Minimal latency, shared resources

### Distributed Deployment
Components run on separate machines:
- Redis: Dedicated instance or cluster
- Worker: Runs on machine(s) where processes will execute
- API: Runs on separate machine(s)
- Change only Redis connection string, no code changes required

### Horizontal Scaling

Run multiple worker instances for load distribution:
- Each worker subscribes to `instance.commands.process` channel
- Redis Pub/Sub delivers messages to all subscribers
- Workers coordinate through shared IInstanceRuntimeStore
- Each worker processes commands independently
- Useful for high availability and load distribution across multiple machines

**Coordination**: Workers check IInstanceRuntimeStore before executing operations to prevent duplicate work and ensure consistency.

## Dependencies

**Core Dependencies**:
- DRaaS.Core - Interfaces, models, messaging contracts
- StackExchange.Redis - Redis client for message bus
- Microsoft.Extensions.Hosting - Worker Service framework
- YamlDotNet - YAML serialization for drasi-server configs

**Runtime Dependencies**:
- Redis server (localhost or remote instance)
- drasi-server executable (in PATH or configured location)

## Message Flow

Creating and starting an instance through the message bus:

```
API Controller or Service
  ├─ Publishes StartInstanceCommand to channel
  │    ├─ Channel: instance.commands.process
  │    ├─ Includes: instanceId, configuration, replyChannel
  │    └─ Waits for response on temporary replyChannel
  ↓
Redis Message Bus
  ├─ Delivers command to all subscribers
  ↓
ProcessCommandWorker (Worker Service)
  ├─ Receives StartInstanceCommand
  ├─ Validates command payload
  ├─ Executes ProcessInstanceManager.StartInstanceAsync()
  │    ├─ Generates YAML config file from Configuration
  │    ├─ Launches drasi-server process with config
  │    └─ Tracks process with PID
  ├─ Publishes StartInstanceResponse to replyChannel
  │    ├─ Contains: success, runtimeInfo, errorMessage
  │    └─ Caller receives response and completes await
  ├─ Publishes InstanceStartedEvent to instance.events
  │    ├─ Broadcast to all subscribers
  │    └─ Contains: instanceId, runtimeInfo
  ↓
Redis Message Bus
  ├─ Delivers event to all subscribers
  ↓
Other Components (Monitoring, Logging, UI)
  └─ Receive InstanceStartedEvent notification
```

**Key Points**:
- Commands use request/response pattern with temporary reply channels
- Events use broadcast pattern for notifications
- Multiple subscribers can receive events simultaneously
- Response channels are automatically cleaned up after use

## Troubleshooting

### Worker not receiving commands

**Symptoms**: Commands published but worker doesn't respond

**Solutions**:
- Check Redis connection string in appsettings.json matches Redis server
- Verify Redis is running: `redis-cli ping` should return `PONG`
- Check logs for subscription errors or connection failures
- Verify channel name matches exactly: `instance.commands.process`
- Test Redis connectivity: `redis-cli -h <host> -p <port> PING`

### Processes not starting

**Symptoms**: StartInstanceCommand received but process doesn't launch

**Solutions**:
- Verify ExecutablePath points to valid drasi-server binary: `which drasi-server` or `where drasi-server`
- Check WorkingDirectory exists and has write permissions
- Check InstanceConfigDirectory exists and has write permissions
- Review generated YAML file in InstanceConfigDirectory for syntax errors
- Check process stdout/stderr logs in WorkingDirectory
- Verify no port conflicts (check if another process is using the allocated port)
- Ensure drasi-server has execute permissions (Linux: `chmod +x drasi-server`)

### Processes exiting unexpectedly

**Symptoms**: ProcessMonitorWorker detects process exit

**Solutions**:
- Check drasi-server logs in working directory
- Review exit code in InstanceStatusChangedEvent logs
- Verify YAML configuration is valid for drasi-server
- Check for resource exhaustion (CPU, memory, disk space)
- Ensure drasi-server dependencies are installed (runtime libraries, frameworks)
- Verify source connections (databases, APIs) are accessible
- Check firewall rules for instance ports

### Redis connection failures

**Symptoms**: Worker crashes on startup with Redis connection error

**Solutions**:
- Verify Redis is accessible from worker machine: `redis-cli -h <host> -p <port> ping`
- Check firewall rules allow Redis port (default 6379)
- Review Redis authentication settings in connection string if applicable
- Check network connectivity between worker and Redis server
- Verify Redis server is not at max connections limit
- Check Redis server logs for connection errors

### Messages not being received

**Symptoms**: Worker subscribes but doesn't process messages

**Solutions**:
- Verify publisher and subscriber use exact same channel name
- Check message serialization (both sides must use same JSON structure)
- Review Redis subscription logs for errors
- Test message flow with redis-cli: `PUBLISH instance.commands.process "test"`
- Check if multiple workers are processing and one consumed the message
- Verify message format matches expected command structure

## Future Enhancements

Planned improvements for the Process platform worker:

- Configuration hot-reload without instance restart
- Process resource limits enforcement (CPU, memory quotas)
- Capture and forward process stdout/stderr to centralized logging system
- HTTP health check polling beyond process alive checks
- Metrics collection and publishing (CPU usage, memory usage, uptime, request counts)
- Automatic restart with exponential backoff on failures
- Support for multiple drasi-server versions running side-by-side
- Process crash dump collection for debugging
- Configuration validation before process launch
- Support for process priority and affinity settings
