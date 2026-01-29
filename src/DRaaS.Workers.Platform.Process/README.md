# DRaaS Workers - Process Platform

This worker service manages Drasi server instances running as native OS processes on the host machine.

## Architecture

The Process Platform Worker is a .NET Worker Service that:

1. **Subscribes to Commands** - Listens to the `instance.commands.process` channel for Start, Stop, Restart, Delete commands
2. **Manages Processes** - Launches and manages drasi-server processes using `System.Diagnostics.Process`
3. **Monitors Health** - Continuously monitors process health and detects unexpected exits
4. **Publishes Events** - Broadcasts instance lifecycle events (Started, Stopped, StatusChanged) to the `instance.events` channel

## Components

### ProcessInstanceManager
- Implements `IDrasiServerInstanceManager` interface from Core
- Manages the lifecycle of drasi-server processes
- Creates instance-specific YAML configuration files
- Tracks running processes in a concurrent dictionary
- Handles graceful shutdown with configurable timeout

### ProcessCommandWorker (BackgroundService)
- Subscribes to `instance.commands.process` channel via Redis message bus
- Handles incoming commands: Start, Stop, Restart, Delete
- Publishes success/failure events back to the message bus
- Executes operations using ProcessInstanceManager

### ProcessMonitorWorker (BackgroundService)
- Polls running processes every 10 seconds
- Detects unexpected process exits
- Updates runtime state when processes fail
- Publishes `InstanceStatusChangedEvent` when status changes

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
  }
}
```

### Redis Connection
- **Single Host**: `"localhost:6379"` - Worker runs on same machine as ControlPlane API
- **Distributed**: `"redis-cluster:6379"` - Worker runs on different machine, shares Redis with other components

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
All components (API, Worker, Redis) run on one machine:
- Redis: localhost:6379
- Worker subscribes to process commands
- API publishes commands to same Redis instance
- Minimal latency, shared memory constraints

### Distributed Deployment
Components run on separate machines:
- Redis: Dedicated Redis cluster
- Worker: Runs on machine(s) where processes will execute
- API: Runs on separate machine(s)
- Change only Redis connection string - no code changes

### Horizontal Scaling
Run multiple instances of this worker:
- Each worker subscribes to `instance.commands.process` channel
- Redis Pub/Sub delivers message to ALL subscribers
- Workers coordinate via IInstanceRuntimeStore (shared state)
- Useful for load distribution across multiple hosts

## Dependencies

- **DRaaS.Core** - Interfaces, models, messaging contracts
- **StackExchange.Redis** - Redis client for message bus
- **Microsoft.Extensions.Hosting** - Worker Service framework

## Message Flow

```
API (ControlPlane)
  ↓ Publishes StartInstanceCommand
Redis (instance.commands.process)
  ↓ Delivers to subscribers
ProcessCommandWorker
  ↓ Executes via ProcessInstanceManager
drasi-server process starts
  ↓ ProcessCommandWorker publishes
Redis (instance.events)
  ↓ Broadcasts InstanceStartedEvent
API / Monitors / Other Subscribers receive event
```

## Troubleshooting

### Worker not receiving commands
- Check Redis connection string
- Verify Redis is running: `redis-cli ping`
- Check logs for subscription errors

### Processes not starting
- Verify `ExecutablePath` points to valid drasi-server executable
- Check `WorkingDirectory` exists and has write permissions
- Check `InstanceConfigDirectory` exists and has write permissions
- Review process stdout/stderr logs

### Processes exiting unexpectedly
- Check ProcessMonitorWorker logs for exit codes
- Review drasi-server logs in working directory
- Verify YAML configuration is valid
- Check for port conflicts (multiple instances same port)

## Future Enhancements

- [ ] Support for process restart with configuration fetch from provider
- [ ] Process resource limits (CPU, memory)
- [ ] Process stdout/stderr capture and forwarding
- [ ] Health check beyond process alive (HTTP endpoint polling)
- [ ] Metrics collection (CPU, memory, uptime)
- [ ] Automatic restart on failure with backoff
