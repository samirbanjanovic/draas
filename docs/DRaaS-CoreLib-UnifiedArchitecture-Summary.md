# DRaaS.CoreLib - Unified Architecture Summary

## Overview
The DRaaS.CoreLib project implements a **unified instance orchestration architecture** that simplifies instance lifecycle management by consolidating domain and infrastructure concerns into a single, cohesive service.

## Architecture Principles

### 1. Single Source of Truth
- **`DrasiInstance`** is the aggregate root containing all instance information
- Infrastructure placement is embedded via **`InstancePlacement`** property
- No separate domain/infrastructure state models to synchronize

### 2. Unified Service
- **`IInstanceOrchestrationService`** manages complete lifecycle
- Replaces the problematic dual-service pattern (domain + orchestration services)
- Automatic state synchronization between domain and infrastructure

### 3. Clear State Model
- **`Status`** enum maps both domain states and infrastructure provider status
- State transitions tracked in `StateHistory` for complete audit trail
- `CurrentStatus` computed property always reflects current state

## Core Components

### Models

#### DrasiInstance (Aggregate Root)
```csharp
public record DrasiInstance
{
    // Identity
    public required string InstanceId { get; init; }
    public required string Name { get; init; }
    public required string[] Owners { get; init; }
    
    // State
    public List<DrasiInstanceState> StateHistory { get; init; }
    public Status CurrentStatus { get; }  // Computed
    
    // Configuration
    public DrasiConfiguration? Configuration { get; init; }
    public Dictionary<string, object?> MetaData { get; init; }
    
    // Infrastructure (embedded)
    public InstancePlacement? Placement { get; init; }  // null if not deployed
}
```

#### InstancePlacement (Value Object)
```csharp
public record InstancePlacement
{
    public required string PlatformType { get; init; }
    public required PlacementProviderRuntimeInfo RuntimeInfo { get; init; }
    public required DateTime PlacedAt { get; init; }
    public DateTime? LastSyncedAt { get; init; }
}
```

#### Status Enum (Unified)
```csharp
public enum Status
{
    // Domain states
    Unknown, Registered, Deregistered,
    
    // Infrastructure states (map to PlacementProviderRuntimeStatus)
    Creating,    // → Deploying
    Created,     // → Deployed
    Starting,    // → Starting
    Running,     // → Running
    Stopping,    // → Stopping
    Stopped,     // → Stopped
    
    // Terminal states
    Deleting, Deleted,
    Error        // → Failed
}
```

#### Command Models
**REMOVED** - Direct parameters used instead of command objects for simplicity.

### Services

#### IInstanceOrchestrationService (Unified Interface)

**Lifecycle Operations:**
- `RegisterInstanceAsync` - Create domain entity (not deployed)
- `DeployInstanceAsync` - Deploy to infrastructure platform
- `StartInstanceAsync` - Start deployed instance
- `StopInstanceAsync` - Stop running instance
- `RestartInstanceAsync` - Stop then start
- `DeleteInstanceAsync` - Remove infrastructure and domain

**Configuration Management:**
- `UpdateInstanceConfigurationAsync` - Update configuration (requires stopped state)
- `UpdateInstanceMetadataAsync` - Update metadata only

**Query Operations:**
- `GetInstanceAsync` - Get with optional state refresh from provider
- `GetInstancesByOwnerAsync` - Query by owner
- `GetAllInstancesAsync` - Get all instances
- `GetInstancesByStatusAsync` - Query by status

**Platform Information:**
- `GetAvailablePlatformsAsync` - List available providers
- `GetDefaultPlatformAsync` - Get default provider

#### InstanceOrchestrationService (Implementation)

**Key Features:**
- Coordinates `IDrasiInstanceStorageService` and `IPlatformInstanceProviderFactory`
- Atomic operations with error rollback
- Automatic state mapping (provider status → domain status)
- State synchronization on demand (`GetInstanceAsync(refreshState: true)`)
- Complete transaction handling (deploy with cleanup on failure)

### Providers

#### IPlatformInstanceProvider
```csharp
public interface IPlatformInstanceProvider 
{
    string PlatformType { get; }
    bool IsAvailable { get; }
    
    Task<PlacementProviderRuntimeInfo> DeployInstanceAsync(...);
    Task<PlacementProviderRuntimeInfo> StartInstanceAsync(...);
    Task<PlacementProviderRuntimeInfo> StopInstanceAsync(...);
    Task DeleteInstanceAsync(...);
    Task<PlacementProviderRuntimeInfo> GetInstanceInfoAsync(...);
}
```

**Implementations:**
- **ProcessInstanceProvider** - Local process-based deployment
- **DockerInstanceProvider** - Docker container deployment (using Docker.DotNet SDK)
- *(Future: KubernetesInstanceProvider, AzureContainerInstancesProvider, etc.)*

#### IPlatformInstanceProviderFactory
```csharp
public interface IPlatformInstanceProviderFactory
{
    IPlatformInstanceProvider DefaultProvider { get; }
    Task<IPlatformInstanceProvider> GetPlatformInstanceProviderAsync(string platformType, ...);
    Task<IEnumerable<IPlatformInstanceProvider>> GetAllPlatformInstanceProvidersAsync(...);
}
```

## Usage Example

```csharp
// 1. Register DI
services.AddInstanceOrchestrationService();
services.AddScoped<IDrasiInstanceStorageService, YourStorageImpl>();
services.AddPlatformProviderFactory(...);

// 2. Inject service
public class InstanceController
{
    private readonly IInstanceOrchestrationService _orchestration;
    
    public InstanceController(IInstanceOrchestrationService orchestration)
    {
        _orchestration = orchestration;
    }
}

// 3. Use unified lifecycle
var command = new RegisterInstanceCommand { ... };

// Register
var instance = await _orchestration.RegisterInstanceAsync(command, ct);
// instance.CurrentStatus == Status.Registered
// instance.Placement == null

// Deploy
instance = await _orchestration.DeployInstanceAsync(instance.InstanceId, "Docker", ct);
// instance.CurrentStatus == Status.Created
// instance.Placement != null

// Start
instance = await _orchestration.StartInstanceAsync(instance.InstanceId, ct);
// instance.CurrentStatus == Status.Running

// Query with state refresh
instance = await _orchestration.GetInstanceAsync(instance.InstanceId, refreshState: true, ct);
// Automatically syncs state from provider

// Stop
instance = await _orchestration.StopInstanceAsync(instance.InstanceId, ct);
// instance.CurrentStatus == Status.Stopped

// Delete
await _orchestration.DeleteInstanceAsync(instance.InstanceId, ct);
// Infrastructure cleaned up, domain marked Deleted
```

## Benefits

### Simplified Application Layer
- Controllers/APIs call single service - no coordination logic
- No manual state synchronization
- Clear, predictable workflows

### Automatic State Management
- `GetInstanceAsync(refreshState: true)` syncs from provider
- Provider status automatically mapped to domain status
- State history provides complete audit trail

### Clear Transaction Boundaries
- Each operation is atomic
- Failures trigger automatic rollback to error state
- Clean error handling with state metadata

### Single Source of Truth
- `DrasiInstance.Placement` contains infrastructure info
- No separate models to keep in sync
- `CurrentStatus` always current

### Extensibility
- Easy to add new providers (just implement `IPlatformInstanceProvider`)
- Provider-agnostic orchestration logic
- Clear separation of concerns

## State Transitions

```
Valid Flows:

Registered → Creating → Created → Starting → Running
                                        ↓
                                   Stopping → Stopped
                                        ↓
                                    Deleting → Deleted

Error Handling:
Any state → Error (on failure)
```

## Files Created/Modified

### New Files
✅ `Models/InstancePlacement.cs` - Infrastructure placement value object  
✅ `Models/Commands/InstanceCommands.cs` - Command objects  
✅ `Services/IInstanceOrchestrationService.cs` - Unified service interface  
✅ `Services/Impl/InstanceOrchestrationService.cs` - Concrete implementation  
✅ `Extensions/InstanceOrchestrationServiceExtensions.cs` - DI registration helper  

### Updated Files
✅ `Models/DrasiInstance.cs` - Added `Placement` property and `CurrentStatus` computed property  
✅ `Models/StatusEnum.cs` - Added XML documentation with provider mapping  

### Removed Files (Obsolete)
❌ `Services/IDrasiInstanceService.cs` - Replaced by unified service  
❌ `Services/IPlatformOrchestrationService.cs` - Replaced by unified service  
❌ `Models/RuntimeInfo.cs` - Replaced by InstancePlacement  

## Project Structure

```
DRaaS.CoreLib/
├── Models/
│   ├── DrasiInstance.cs                    # Aggregate root
│   ├── InstancePlacement.cs                # NEW: Infrastructure placement
│   ├── StatusEnum.cs                       # Enhanced with docs
│   ├── DrasiInstanceState.cs              # State history entry
│   ├── Commands/
│   │   └── InstanceCommands.cs            # NEW: Command objects
│   ├── PlacementProviderRuntimeInfo.cs
│   ├── PlacementProviderRuntimeStatusEnum.cs
│   ├── InstanceDeploymentInfo.cs
│   ├── DrasiConfiguration.cs
│   ├── Source.cs
│   ├── Query.cs
│   └── Reaction.cs
├── Services/
│   ├── IInstanceOrchestrationService.cs    # NEW: Unified service
│   ├── Impl/
│   │   └── InstanceOrchestrationService.cs # NEW: Implementation
│   └── IDrasiInstanceStorageService.cs
├── Providers/
│   ├── IPlatformInstanceProvider.cs
│   ├── IPlatformInstanceProviderFactory.cs
│   ├── IDrasiConfigurationProvider.cs
│   ├── Impl/
│   │   ├── ProcessInstanceProvider.cs
│   │   ├── DockerInstanceProvider.cs
│   │   └── YamlDrasiConfigurationProvider.cs
│   ├── Abstractions/
│   │   └── IDraasDockerClient.cs
│   └── Extensions/
│       └── DockerProviderServiceExtensions.cs
├── Clients/
│   └── Docker/
│       └── DockerSdkClient.cs
├── StateMachines/
│   └── ProviderStateMachine.cs
└── Extensions/
    └── InstanceOrchestrationServiceExtensions.cs  # NEW: DI helper
```

## Testing Strategy

### Unit Tests
```csharp
var mockStorage = new Mock<IDrasiInstanceStorageService>();
var mockFactory = new Mock<IPlatformInstanceProviderFactory>();

var service = new InstanceOrchestrationService(
    mockStorage.Object,
    mockFactory.Object);

// Test lifecycle operations with mocks
```

### Integration Tests
```csharp
services.AddInstanceOrchestrationService();
services.AddSingleton<IDrasiInstanceStorageService, InMemoryStorage>();
services.AddPlatformProviderFactory(options => {
    options.AddProcessProvider();
    options.AddDockerProvider();
});

var orchestration = provider.GetRequiredService<IInstanceOrchestrationService>();
// Test complete lifecycle with real providers
```

## Migration Notes

**BREAKING CHANGES:**
- Old interfaces removed: `IDrasiInstanceService`, `IPlatformOrchestrationService`
- `RuntimeInfo` model removed (use `InstancePlacement` instead)
- All code must use `IInstanceOrchestrationService`

**Note:** The DRaaS.Core project is separate and unaffected by these changes. It maintains its own implementation.

## Future Enhancements

- [ ] Background state synchronization service
- [ ] State machine validation using `ProviderStateMachine`
- [ ] Health checks and automatic recovery
- [ ] Scaling support (replicas)
- [ ] Configuration rollback
- [ ] Metrics and observability
- [ ] Additional providers (Kubernetes, ACI, ECS, etc.)

## Summary

The unified architecture successfully:
1. ✅ Eliminates dual-service state synchronization issues
2. ✅ Provides single source of truth (`DrasiInstance` with embedded `InstancePlacement`)
3. ✅ Simplifies application layer (no controller coordination)
4. ✅ Enables automatic state management
5. ✅ Maintains clear transaction boundaries
6. ✅ Supports extensibility for new providers
7. ✅ Provides complete audit trail via state history

**Result:** Clean, maintainable architecture ready for production use in DRaaS.CoreLib.
