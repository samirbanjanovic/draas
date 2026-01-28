# ProcessInstanceManager Configuration

The `ProcessInstanceManager` launches and manages drasi-server instances as local processes. It requires configuration for the executable path and runtime settings.

## Configuration

Add the following section to `appsettings.json` in the ControlPlane project:

```json
{
  "ProcessInstanceManager": {
    "ExecutablePath": "drasi-server",
    "ConfigurationTemplatePath": "",
    "InstanceConfigDirectory": "./drasi-configs",
    "DefaultLogLevel": "info",
    "ShutdownTimeoutSeconds": 5,
    "WorkingDirectory": "./drasi-runtime"
  }
}
```

### Configuration Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ExecutablePath` | string | `"drasi-server"` | Path to the drasi-server executable. Can be absolute or relative path, or just the executable name if it's in PATH |
| `ConfigurationTemplatePath` | string | `""` | (Reserved for future use) Path to a YAML template file |
| `InstanceConfigDirectory` | string | `"./drasi-configs"` | Directory where instance-specific YAML config files will be created |
| `DefaultLogLevel` | string | `"info"` | Default log level for drasi-server instances (trace, debug, info, warn, error) |
| `ShutdownTimeoutSeconds` | int | `5` | Timeout in seconds to wait for graceful shutdown before force kill |
| `WorkingDirectory` | string | `"./drasi-runtime"` | Working directory for drasi-server processes |

## How It Works

When you create a DRaaS instance using the Process platform:

1. **Configuration Retrieval**: The `Configuration` is retrieved from the configuration store (contains sources, queries, reactions)

2. **YAML Generation**: ProcessInstanceManager generates a drasi-server YAML config file in the `InstanceConfigDirectory`:
   ```
   ./drasi-configs/
   └── {instanceId}-config.yaml
   ```

3. **Process Launch**: The drasi-server is started with:
   ```bash
   drasi-server --config ./drasi-configs/{instanceId}-config.yaml
   ```

4. **Process Management**: The manager tracks the process and monitors its status

## Generated YAML Structure

The generated YAML configuration follows the [drasi-server format](https://github.com/samirbanjanovic/drasi-server):

```yaml
id: {instanceId}
host: 0.0.0.0
port: 8080
logLevel: info
persistConfig: true
persistIndex: false

sources:
  - kind: postgres
    id: my-source
    autoStart: true
    # source-specific configuration

queries:
  - id: my-query
    query: |
      MATCH (n:Node)
      RETURN n
    sources:
      - sourceId: my-source

reactions:
  - kind: log
    id: my-reaction
    queries: [my-query]
```

## Prerequisites

1. **drasi-server Binary**: Ensure the drasi-server executable is available:
   - Either in your system PATH
   - Or specify the full path in `ExecutablePath`

2. **Permissions**: The process must have:
   - Execute permissions for the drasi-server binary
   - Read/write permissions for `InstanceConfigDirectory` and `WorkingDirectory`

3. **Port Availability**: The `IPortAllocator` service will assign available ports for each instance

## Example: Creating an Instance

```bash
# Create instance via ControlPlane API
curl -X POST http://localhost:5000/api/servers \
  -H "Content-Type: application/json" \
  -d '{
    "instanceId": "my-drasi-instance",
    "platform": "Process",
    "configuration": {
      "host": "127.0.0.1",
      "port": 8080,
      "logLevel": "info",
      "sources": [
        {
          "kind": "mock",
          "id": "test-source",
          "autoStart": true
        }
      ],
      "queries": [
        {
          "id": "test-query",
          "queryText": "MATCH (n) RETURN n",
          "sources": [
            { "sourceId": "test-source" }
          ]
        }
      ],
      "reactions": [
        {
          "kind": "log",
          "id": "log-reaction",
          "queries": ["test-query"]
        }
      ]
    }
  }'
```

This will:
1. Allocate a port (e.g., 8080)
2. Generate `./drasi-configs/my-drasi-instance-config.yaml`
3. Launch `drasi-server --config ./drasi-configs/my-drasi-instance-config.yaml`
4. Track the process with PID
5. Store runtime info in the InstanceRuntimeStore

## Troubleshooting

### "drasi-server not found"

Ensure the executable is in your PATH or specify the full path:

```json
{
  "ProcessInstanceManager": {
    "ExecutablePath": "/usr/local/bin/drasi-server"
  }
}
```

### Process won't start

Check:
- Execute permissions on the binary
- Configuration directories exist and are writable
- Port is not already in use

### Graceful shutdown timeout

Increase the timeout if your instances need more time to shut down:

```json
{
  "ProcessInstanceManager": {
    "ShutdownTimeoutSeconds": 30
  }
}
```

## Monitoring

Process status is monitored by `ProcessStatusMonitor`, which:
- Polls process health every 30 seconds
- Detects crashed processes
- Publishes status changes to the centralized `StatusUpdateService`
- Triggers reconciliation when drift is detected

## Related Documentation

- [drasi-server Documentation](https://github.com/samirbanjanovic/drasi-server)
- [drasi-server Configuration Reference](https://github.com/samirbanjanovic/drasi-server#configuration-reference)
- [DRaaS Architecture](../../README.md)
