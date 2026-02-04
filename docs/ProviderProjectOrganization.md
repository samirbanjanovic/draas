# Provider Project Organization Evaluation

## Current State
All providers currently live in `DRaaS.CoreLib/Providers/Impl/`:
- `ProcessInstanceProvider`
- `DockerInstanceProvider`
- (Future: KubernetesInstanceProvider, AzureContainerInstanceProvider, etc.)

## Question
Should providers be moved to a separate project (e.g., `DRaaS.Providers`)?

---

## Option 1: Keep Providers in CoreLib (RECOMMENDED)

### Pros
1. **Domain boundaries unclear** - We're still learning the system's natural boundaries
2. **Avoid project hell** - Too many small projects create maintenance overhead
3. **Single compilation unit** - Faster builds, simpler development workflow
4. **Natural evolution** - Can refactor once patterns emerge (3-4 providers implemented)
5. **Providers are infrastructure** - They implement CoreLib interfaces, so they're tightly coupled anyway
6. **Deployment simplicity** - Single library to reference

### Cons
1. **Dependency mixing** - Docker SDK, Process, K8s SDK, Azure SDK all in one project
2. **Larger binary** - If someone only wants Process provider, they get Docker dependencies too
3. **Versioning** - Can't version providers independently

### Mitigation Strategies
- Use **clear folder structure**: 
  - `/Providers/IPlatformInstanceProvider.cs`
  - `/Providers/Abstractions/` (IDockerClient, IKubernetesClient, etc.)
  - `/Providers/Impl/Process/`
  - `/Providers/Impl/Docker/`
  - `/Providers/Impl/Kubernetes/`
- Use **interfaces for external dependencies** (IDockerClient, IProcessRunner)
- **Defer refactoring** until we have 3-4 providers and understand common patterns

---

## Option 2: Separate Providers Project

### Structure
```
DRaaS.CoreLib/
  ├── Models/
  ├── Services/
  ├── StateMachines/
  └── Providers/
      ├── IPlatformInstanceProvider.cs
      └── IDrasiConfigurationProvider.cs

DRaaS.Providers/  (new project)
  ├── Process/
  │   └── ProcessInstanceProvider.cs
  ├── Docker/
  │   ├── DockerInstanceProvider.cs
  │   └── Abstractions/
  │       └── IDockerClient.cs
  └── Kubernetes/
      └── KubernetesInstanceProvider.cs
```

### Pros
1. **Cleaner separation** - Providers isolated from core domain
2. **Independent versioning** - Update Docker provider without touching core
3. **Lighter dependencies** - Core doesn't reference Docker/K8s SDKs
4. **Plugin architecture** - Could load providers dynamically

### Cons
1. **Premature abstraction** - Don't know domain boundaries yet
2. **Project proliferation** - More projects = more maintenance, more builds
3. **Cross-project changes** - Interface changes require updating multiple projects
4. **Deployment complexity** - Multiple NuGet packages to manage

---

## Recommendation: Keep in CoreLib (For Now)

### Reasoning
1. **We're in exploration phase** - Still learning what providers need
2. **Avoid premature optimization** - "Project hell" is harder to undo than consolidation
3. **Patterns emerge from implementation** - Need 3-4 providers to see commonalities
4. **Refactoring is easier** - Can split later when boundaries are clear

### When to Revisit
Consider moving to separate project when:
- ✅ We have 3-4 working providers
- ✅ Clear patterns emerge (shared abstractions, common infrastructure)
- ✅ Dependency conflicts arise (e.g., Docker SDK conflicts with K8s SDK)
- ✅ We want plugin/extension model for community providers
- ✅ Independent versioning becomes necessary (e.g., security patch for Docker provider only)

### Immediate Actions
1. ✅ Create `Providers/Abstractions/` folder for client interfaces (IDockerClient, etc.)
2. ✅ Organize by provider type: `Providers/Impl/Process/`, `Providers/Impl/Docker/`
3. ✅ Use dependency injection for external dependencies (IDockerClient, not Docker CLI directly)
4. ⏳ Implement 2-3 more providers to understand patterns
5. ⏳ Revisit decision once patterns are clear

---

## Alternative: Hybrid Approach

Keep abstractions in CoreLib, implementations in separate project:

```
DRaaS.CoreLib/
  └── Providers/
      ├── IPlatformInstanceProvider.cs
      └── Abstractions/
          ├── IDockerClient.cs
          └── IKubernetesClient.cs

DRaaS.Providers.Process/
DRaaS.Providers.Docker/
DRaaS.Providers.Kubernetes/
```

**Assessment**: Overkill for current phase. Consider this approach if building a provider ecosystem.

---

## Decision
**Keep providers in DRaaS.CoreLib until we have 3-4 implementations and understand domain boundaries.**

Benefits:
- Faster iteration
- Less overhead
- Natural evolution
- Easy to refactor later

Risks (mitigated):
- Dependency bloat → Use abstractions (IDockerClient)
- Tight coupling → Clear folder structure
- Hard to split later → Not true - C# refactoring tools make project splits easy

---

## Interop Abstraction (Separate Concern)

The Docker CLI anti-pattern is **orthogonal to project organization**.

Whether providers live in CoreLib or separate project, we need:
- ✅ `IDockerClient` interface
- ✅ `DockerCliClient` implementation (wraps CLI calls)
- ✅ Option to swap for `Docker.DotNet` later
- ✅ Mockable for unit tests

This abstraction should live in:
- **If keeping providers in CoreLib**: `DRaaS.CoreLib/Providers/Abstractions/`
- **If splitting providers**: `DRaaS.Providers.Docker/Abstractions/`

Since we're keeping providers in CoreLib, the abstraction lives there too.
