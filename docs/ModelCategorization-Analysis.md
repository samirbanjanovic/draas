# DRaaS.CoreLib Model Categorization & Analysis

## Model Inventory by Category

### 📦 DOMAIN / AGGREGATE ROOT
**Purpose:** Core business entities representing the domain

| Model | File | Description | Key Properties | Notes |
|-------|------|-------------|----------------|-------|
| **DrasiInstance** | `Models/DrasiInstance.cs` | Aggregate root for Drasi instances | `InstanceId`, `Name`, `Description`, `Owners`, `Configuration`, `MetaData`, `StateHistory`, `Placement` | ✅ Core domain entity |

---

### 🔧 INFRASTRUCTURE / PLACEMENT
**Purpose:** Infrastructure and deployment information

| Model | File | Description | Key Properties | Notes |
|-------|------|-------------|----------------|-------|
| **InstancePlacement** | `Models/InstancePlacement.cs` | Infrastructure placement wrapper | `PlatformType`, `RuntimeInfo`, `PlacedAt`, `LastSyncedAt` | ⚠️ WRAPPER - Contains duplicate `PlatformType` |
| **PlacementProviderRuntimeInfo** | `Models/PlacementProviderRuntimeInfo.cs` | Runtime info from providers | `InstanceId`, `PlatformType`, `Status`, `DeployedAt`, `StartedAt`, `StoppedAt`, `PlatformMetadata` | ✅ Provider contract |

**⚠️ DUPLICATE ANALYSIS:**
- `InstancePlacement.PlatformType` duplicates `PlacementProviderRuntimeInfo.PlatformType`
- `InstancePlacement.PlacedAt` similar to `PlacementProviderRuntimeInfo.DeployedAt`
- **RECOMMENDATION:** Merge `InstancePlacement` into `PlacementProviderRuntimeInfo`

---

### 📋 STATE MANAGEMENT
**Purpose:** Track state and status over time

| Model | File | Description | Key Properties | Notes |
|-------|------|-------------|----------------|-------|
| **Status** (enum) | `Models/StatusEnum.cs` | Domain instance status | `Unknown`, `Registered`, `Deregistered`, `Creating`, `Created`, `Starting`, `Running`, `Stopping`, `Stopped`, `Deleting`, `Deleted`, `Error` | ✅ Unified domain status |
| **PlacementProviderRuntimeStatus** (enum) | `Models/PlacementProviderRuntimeStatusEnum.cs` | Provider infrastructure status | `Unknown`, `Deploying`, `Deployed`, `Starting`, `Running`, `Stopping`, `Stopped`, `Failed`, `Deleted` | ✅ Provider boundary status |
| **DrasiInstanceState** | `Models/DrasiInstanceState.cs` | State history entry | `Status`, `TimeStamp`, `StateMetadata` | ✅ Audit trail |

**✅ ANALYSIS:**
- Two status enums are JUSTIFIED - maintain domain/provider boundary
- `DrasiInstanceState` is clean audit trail mechanism
- **RECOMMENDATION:** Keep as-is

---

### ⚙️ CONFIGURATION
**Purpose:** Drasi-specific configuration data

| Model | File | Description | Key Properties | Notes |
|-------|------|-------------|----------------|-------|
| **DrasiConfiguration** | `Models/DrasiConfiguration.cs` | Complete Drasi config | `Sources`, `Queries`, `Reactions` | ✅ Configuration aggregate |
| **Source** | `Models/Source.cs` | Data source configuration | Source-specific properties | ✅ Config component |
| **Query** | `Models/Query.cs` | Query configuration | Query-specific properties | ✅ Config component |
| **Reaction** | `Models/Reaction.cs` | Reaction configuration | Reaction-specific properties | ✅ Config component |

**✅ ANALYSIS:**
- Clean composition: `DrasiConfiguration` contains `Source[]`, `Query[]`, `Reaction[]`
- **RECOMMENDATION:** Keep as-is

---

### 📨 INPUT/OUTPUT DTOs
**Purpose:** Data transfer between layers

| Model | File | Description | Key Properties | Notes |
|-------|------|-------------|----------------|-------|
| **InstanceDeploymentInfo** | `Models/InstanceDeploymentInfo.cs` | Deployment input to providers | `InstanceId`, `Name`, `Configuration`, `DeploymentMetadata` | ⚠️ REDUNDANT - Just maps fields from DrasiInstance |

**⚠️ REDUNDANT ANALYSIS:**
```csharp
// Current usage in InstanceOrchestrationService:
var deploymentInfo = new InstanceDeploymentInfo
{
    InstanceId = instance.InstanceId,        // From DrasiInstance
    Name = instance.Name,                     // From DrasiInstance
    Configuration = instance.Configuration!   // From DrasiInstance
};
runtimeInfo = await provider.DeployInstanceAsync(deploymentInfo, ct);
```

**RECOMMENDATION:** Remove `InstanceDeploymentInfo`, pass parameters directly:
```csharp
runtimeInfo = await provider.DeployInstanceAsync(
    instance.InstanceId,
    instance.Name,
    instance.Configuration!,
    ct);
```

---

## Summary Matrix

| Category | Model Count | Status | Simplification Opportunity |
|----------|-------------|--------|---------------------------|
| **Domain** | 1 | ✅ Clean | None |
| **Infrastructure** | 2 | ⚠️ Wrapper | Merge `InstancePlacement` into `PlacementProviderRuntimeInfo` |
| **State Management** | 3 | ✅ Clean | None |
| **Configuration** | 4 | ✅ Clean | None |
| **DTOs** | 1 | ⚠️ Redundant | Remove `InstanceDeploymentInfo` |
| **TOTAL** | 11 | | **-2 models** after simplification |

---

## Duplicate Field Analysis

### 🔴 CRITICAL DUPLICATES

| Field | Location 1 | Location 2 | Impact |
|-------|-----------|-----------|--------|
| `PlatformType` | `InstancePlacement.PlatformType` | `PlacementProviderRuntimeInfo.PlatformType` | Must stay in sync |
| `PlacedAt` / `DeployedAt` | `InstancePlacement.PlacedAt` | `PlacementProviderRuntimeInfo.DeployedAt` | Similar semantics, confusing |
| `InstanceId` | `DrasiInstance.InstanceId` | `PlacementProviderRuntimeInfo.InstanceId` | Acceptable - foreign key reference |
| `InstanceId` | `DrasiInstance.InstanceId` | `InstanceDeploymentInfo.InstanceId` | Redundant DTO field |
| `Name` | `DrasiInstance.Name` | `InstanceDeploymentInfo.Name` | Redundant DTO field |
| `Configuration` | `DrasiInstance.Configuration` | `InstanceDeploymentInfo.Configuration` | Redundant DTO field |

---

## Model Relationship Diagram

```
┌─────────────────────────────────────────────────────────────┐
│ DOMAIN LAYER                                                 │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  DrasiInstance (Aggregate Root)                             │
│  ├── InstanceId                                             │
│  ├── Name                                                   │
│  ├── Description                                            │
│  ├── Owners[]                                               │
│  ├── CreatedAt / LastUpdatedAt                             │
│  ├── StateHistory: Stack<DrasiInstanceState>               │
│  │   └── DrasiInstanceState                                │
│  │       ├── Status (enum)                                 │
│  │       ├── TimeStamp                                     │
│  │       └── StateMetadata                                 │
│  ├── Configuration: DrasiConfiguration                      │
│  │   ├── Sources: Source[]                                 │
│  │   ├── Queries: Query[]                                  │
│  │   └── Reactions: Reaction[]                             │
│  ├── MetaData                                              │
│  └── Placement: InstancePlacement ⚠️ WRAPPER               │
│      ├── PlatformType (DUPLICATE)                          │
│      ├── PlacedAt (DUPLICATE)                              │
│      ├── LastSyncedAt                                      │
│      └── RuntimeInfo: PlacementProviderRuntimeInfo         │
│          ├── InstanceId                                    │
│          ├── PlatformType (DUPLICATE)                      │
│          ├── Status: PlacementProviderRuntimeStatus (enum) │
│          ├── DeployedAt (DUPLICATE as PlacedAt)            │
│          ├── StartedAt                                     │
│          ├── StoppedAt                                     │
│          └── PlatformMetadata                              │
│                                                             │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ INFRASTRUCTURE LAYER (Providers)                            │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  IPlatformInstanceProvider                                  │
│  └── Methods accept/return PlacementProviderRuntimeInfo     │
│                                                              │
│  Currently also receives:                                   │
│  └── InstanceDeploymentInfo ⚠️ REDUNDANT DTO               │
│      ├── InstanceId (from DrasiInstance)                   │
│      ├── Name (from DrasiInstance)                         │
│      ├── Configuration (from DrasiInstance)                │
│      └── DeploymentMetadata (rarely used)                  │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## Simplification Recommendations

### 🔥 HIGH PRIORITY: Remove InstancePlacement Wrapper

**Current Structure:**
```
DrasiInstance
  └── Placement: InstancePlacement
      ├── PlatformType (DUPLICATE)
      ├── PlacedAt (DUPLICATE)
      ├── LastSyncedAt
      └── RuntimeInfo: PlacementProviderRuntimeInfo
          ├── PlatformType (DUPLICATE)
          ├── DeployedAt (= PlacedAt)
          ├── StartedAt
          ├── StoppedAt
          └── ...
```

**Simplified Structure:**
```
DrasiInstance
  └── Placement: PlacementProviderRuntimeInfo
      ├── PlatformType (single)
      ├── DeployedAt (single, renamed from PlacedAt)
      ├── StartedAt
      ├── StoppedAt
      ├── LastSyncedAt (moved here)
      └── ...
```

**Benefits:**
- ✅ Eliminates 1 class
- ✅ Removes 2 duplicate fields
- ✅ Simpler access: `instance.Placement.Status` vs `instance.Placement.RuntimeInfo.Status`
- ✅ No semantic loss

**Impact:** Moderate refactor, high value

---

### 🔥 HIGH PRIORITY: Remove InstanceDeploymentInfo

**Current:**
```csharp
var deploymentInfo = new InstanceDeploymentInfo
{
    InstanceId = instance.InstanceId,
    Name = instance.Name,
    Configuration = instance.Configuration!
};
await provider.DeployInstanceAsync(deploymentInfo, ct);
```

**Simplified:**
```csharp
await provider.DeployInstanceAsync(
    instance.InstanceId,
    instance.Name,
    instance.Configuration!,
    ct);
```

**Benefits:**
- ✅ Eliminates 1 class
- ✅ Removes mapping code
- ✅ Clearer method signature
- ✅ No semantic loss

**Impact:** Simple refactor, immediate value

---

### ✅ KEEP AS-IS: Status Enums

**Status** (domain) and **PlacementProviderRuntimeStatus** (provider) serve different purposes:
- `Status` represents domain lifecycle (Registered, Deregistered, etc.)
- `PlacementProviderRuntimeStatus` represents infrastructure state
- Mapping between them maintains clean boundaries

**KEEP SEPARATE**

---

### ✅ KEEP AS-IS: Configuration Models

**DrasiConfiguration**, **Source**, **Query**, **Reaction** are clean composition:
- Clear hierarchy
- No duplication
- Good separation

**KEEP AS-IS**

---

### ✅ KEEP AS-IS: State Management

**DrasiInstanceState** with **Stack<DrasiInstanceState>**:
- Clean audit trail
- Optimized with Stack (O(1) access)
- No duplication

**KEEP AS-IS**

---

## Final Model Count

| | Before | After | Change |
|---|--------|-------|--------|
| **Total Models** | 11 | 9 | -2 (-18%) |
| **Duplicate Fields** | 6 | 1 | -5 (-83%) |
| **Wrapper Classes** | 2 | 0 | -2 (-100%) |
| **Nesting Levels** | 3 | 2 | -1 (-33%) |

---

## Recommended Refactoring Order

### Phase 1: Remove InstanceDeploymentInfo ⚡ (30 min)
1. Update `IPlatformInstanceProvider.DeployInstanceAsync` signature
2. Update `ProcessInstanceProvider` implementation
3. Update `DockerInstanceProvider` implementation  
4. Update `InstanceOrchestrationService` call site
5. Delete `Models/InstanceDeploymentInfo.cs`

### Phase 2: Merge InstancePlacement into PlacementProviderRuntimeInfo ⚡ (45 min)
1. Add `LastSyncedAt` to `PlacementProviderRuntimeInfo`
2. Convert `PlacementProviderRuntimeInfo` to record
3. Update `DrasiInstance.Placement` type
4. Update `InstanceOrchestrationService` creation/update code
5. Delete `Models/InstancePlacement.cs`

### Phase 3: Documentation Update 📝 (15 min)
1. Update architecture docs
2. Update API examples
3. Update README

**Total Effort:** ~90 minutes  
**Risk:** Low (compile-time safety)  
**Value:** High (significant simplification)

---

## Category Summary

```
📦 Domain (1 model)
   └── DrasiInstance ✅

🔧 Infrastructure (2 models → 1 after merge)
   ├── InstancePlacement ⚠️ REMOVE
   └── PlacementProviderRuntimeInfo ✅

📋 State (3 models)
   ├── Status (enum) ✅
   ├── PlacementProviderRuntimeStatus (enum) ✅
   └── DrasiInstanceState ✅

⚙️ Configuration (4 models)
   ├── DrasiConfiguration ✅
   ├── Source ✅
   ├── Query ✅
   └── Reaction ✅

📨 DTOs (1 model → 0 after removal)
   └── InstanceDeploymentInfo ⚠️ REMOVE
```

**Final: 9 clean, focused models (from 11)**
