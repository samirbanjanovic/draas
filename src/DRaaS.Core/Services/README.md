# Services Folder Organization

The Services folder is organized by **functional domain** to improve maintainability, discoverability, and separation of concerns.

## Folder Structure

```
Services/
├── Instance/               # Instance lifecycle management
│   ├── IDrasiInstanceService.cs
│   └── DrasiInstanceService.cs
│
├── Storage/                # Runtime state persistence
│   ├── IInstanceRuntimeStore.cs
│   └── InMemoryInstanceRuntimeStore.cs
│
├── ResourceAllocation/     # Port and resource management
│   ├── IPortAllocator.cs
│   └── PortAllocator.cs
│
├── Monitoring/             # Status monitoring and events
│   ├── IStatusUpdateService.cs
│   ├── StatusUpdateService.cs
│   ├── IStatusMonitor.cs
│   └── ProcessStatusMonitor.cs
│
├── Orchestration/          # Platform selection and coordination
│   ├── IPlatformOrchestratorService.cs
│   └── PlatformOrchestratorService.cs
│
└── Factory/                # Factory patterns
    ├── IInstanceManagerFactory.cs
    └── InstanceManagerFactory.cs
```

## Domain Descriptions

### 📦 Instance
**Purpose**: Manages the lifecycle of Drasi instance metadata

**Key Responsibilities**:
- Create, retrieve, update, delete instance records
- Track instance status (Created, Running, Stopped, Error)
- Store instance metadata (name, description, platform type)

**Key Classes**:
- `IDrasiInstanceService`: Interface for instance operations
- `DrasiInstanceService`: In-memory implementation

**Dependencies**: 
- Orchestration (for platform selection)

---

### 💾 Storage
**Purpose**: Abstracts runtime state persistence

**Key Responsibilities**:
- Save and retrieve runtime information
- Support filtering by platform type
- Provide pluggable storage implementations

**Key Classes**:
- `IInstanceRuntimeStore`: Storage abstraction interface
- `InMemoryInstanceRuntimeStore`: In-memory implementation

**Future Implementations**:
- `CosmosDbInstanceRuntimeStore`
- `SqlInstanceRuntimeStore`
- `RedisInstanceRuntimeStore`

**Dependencies**: None (pure storage abstraction)

---

### 🔌 ResourceAllocation
**Purpose**: Manages shared resources across platform managers

**Key Responsibilities**:
- Allocate and release ports
- Track allocated resources to prevent conflicts
- Thread-safe resource management

**Key Classes**:
- `IPortAllocator`: Port allocation interface
- `PortAllocator`: Thread-safe port allocator (8080-9000 range)

**Dependencies**: None

---

### 📊 Monitoring
**Purpose**: Bidirectional status management for instances

**Key Responsibilities**:
- Centralized status update bus
- Support both polling (Process) and push (Docker/AKS) patterns
- Publish status change events
- Monitor process health

**Key Classes**:
- `IStatusUpdateService`: Centralized status bus
- `StatusUpdateService`: Event-driven status publisher
- `IStatusMonitor`: Monitoring strategy interface
- `ProcessStatusMonitor`: Polling-based monitor for local processes

**Patterns**:
- **Process (Polling)**: Monitor checks every 5 seconds
- **Docker/AKS (Push)**: External daemons POST to API endpoint

**Dependencies**:
- Storage (read/write runtime status)

---

### 🎭 Orchestration
**Purpose**: Platform selection and resource coordination

**Key Responsibilities**:
- Select appropriate platform for new instances
- Delegate resource allocation to platform managers
- Release resources when instances are deleted
- Provide default platform fallback

**Key Classes**:
- `IPlatformOrchestratorService`: Orchestration interface
- `PlatformOrchestratorService`: Delegates to managers

**Future Enhancements**:
- Load-based platform selection
- Cost-based platform selection
- Capability-based routing

**Dependencies**:
- ResourceAllocation (provides port allocator to managers)
- Factory (retrieves platform managers)

---

### 🏭 Factory
**Purpose**: Factory pattern for platform manager selection

**Key Responsibilities**:
- Register all available platform managers
- Retrieve manager by PlatformType enum
- Provide default platform manager
- List all registered platforms

**Key Classes**:
- `IInstanceManagerFactory`: Factory interface
- `InstanceManagerFactory`: Dictionary-based lookup

**Dependencies**:
- Providers (IDrasiServerInstanceManager)

---

## Dependency Flow

```
┌─────────────────────────────────────────────────────────┐
│                     Application Layer                    │
│              (Controllers, Program.cs)                   │
└─────────────┬───────────────────────────────────────────┘
              │
              ├─► Instance ────► Orchestration ──┬─► Factory ──► Providers
              │                                  │
              ├─► Monitoring ──► Storage         └─► ResourceAllocation
              │
              └─► Storage
```

### Key Insights:
1. **Storage** has no dependencies (pure abstraction)
2. **ResourceAllocation** has no dependencies (shared utility)
3. **Monitoring** depends on Storage
4. **Instance** depends on Orchestration
5. **Orchestration** depends on Factory and ResourceAllocation
6. **Factory** depends on Providers (outside Services folder)

---

## Usage Examples

### Creating an Instance
```csharp
using DRaaS.Core.Services.Instance;
using DRaaS.Core.Services.Orchestration;

var instanceService = new DrasiInstanceService(orchestrator);
var instance = await instanceService.CreateInstanceAsync("my-instance");
```

### Allocating Resources
```csharp
using DRaaS.Core.Services.Orchestration;
using DRaaS.Core.Services.ResourceAllocation;
using DRaaS.Core.Services.Factory;

var orchestrator = new PlatformOrchestratorService(portAllocator, factory);
var config = await orchestrator.AllocateResourcesAsync(PlatformType.Process);
```

### Monitoring Status
```csharp
using DRaaS.Core.Services.Monitoring;

var statusService = new StatusUpdateService(runtimeStore);
statusService.StatusChanged += (sender, e) =>
{
    Console.WriteLine($"{e.InstanceId}: {e.OldStatus} → {e.NewStatus}");
};
```

### Storing Runtime Info
```csharp
using DRaaS.Core.Services.Storage;

var store = new InMemoryInstanceRuntimeStore();
await store.SaveAsync(runtimeInfo);
var info = await store.GetAsync(instanceId);
```

---

## Namespace Convention

All services use the pattern: `DRaaS.Core.Services.<Domain>`

- `DRaaS.Core.Services.Instance`
- `DRaaS.Core.Services.Storage`
- `DRaaS.Core.Services.ResourceAllocation`
- `DRaaS.Core.Services.Monitoring`
- `DRaaS.Core.Services.Orchestration`
- `DRaaS.Core.Services.Factory`

When importing, use specific namespaces:

```csharp
using DRaaS.Core.Services.Instance;
using DRaaS.Core.Services.Storage;
using DRaaS.Core.Services.Monitoring;
```

Avoid the generic `using DRaaS.Core.Services;` import.

---

## Adding New Services

When adding new services, follow this process:

1. **Identify the domain**: Does it fit Instance, Storage, Monitoring, Orchestration, ResourceAllocation, or Factory?
2. **Create interface first**: Start with `I{ServiceName}.cs`
3. **Implement the service**: Create `{ServiceName}.cs`
4. **Update namespace**: Use `DRaaS.Core.Services.<Domain>`
5. **Document dependencies**: Update this README if new dependencies are introduced
6. **Register in DI**: Add to `Program.cs` with appropriate lifetime

### Example: Adding CosmosDB Storage

```
Services/Storage/
├── IInstanceRuntimeStore.cs          (existing)
├── InMemoryInstanceRuntimeStore.cs   (existing)
└── CosmosDbInstanceRuntimeStore.cs   (new)
```

```csharp
namespace DRaaS.Core.Services.Storage;

public class CosmosDbInstanceRuntimeStore : IInstanceRuntimeStore
{
    // Implementation
}
```

---

## Design Principles

1. **Interface Segregation**: Small, focused interfaces
2. **Single Responsibility**: Each domain has one clear purpose
3. **Dependency Inversion**: Depend on abstractions (interfaces)
4. **Open/Closed**: Open for extension, closed for modification
5. **Explicit Dependencies**: Import specific namespaces, not generic ones

---

## Benefits

✅ **Discoverability**: Easy to find services by domain
✅ **Maintainability**: Changes isolated to specific domains
✅ **Testability**: Mock entire domains for testing
✅ **Scalability**: Add new implementations without restructuring
✅ **Clarity**: Clear separation of concerns

---

## Migration Notes

If you have code referencing the old `using DRaaS.Core.Services;`, update to:

```csharp
// Old (generic)
using DRaaS.Core.Services;

// New (specific)
using DRaaS.Core.Services.Instance;
using DRaaS.Core.Services.Storage;
using DRaaS.Core.Services.Monitoring;
using DRaaS.Core.Services.Orchestration;
using DRaaS.Core.Services.ResourceAllocation;
using DRaaS.Core.Services.Factory;
```

The build will guide you with compilation errors if namespaces are missing.
