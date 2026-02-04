# Platform Record - Common Properties

## Design Philosophy

The `Platform` record balances **common properties** (applicable to 99% of platforms) with **extensibility** (platform-specific metadata).

This design is influenced by patterns from:
- **Kubernetes** (capacity, health, labels)
- **DAPR** (version, endpoints, scopes)
- **RADIUS** (environment, location, connections)

---

## Common Properties (99% Applicable)

### 1. **InstanceCount** (int?)
Current number of instances deployed on this platform.

**Examples:**
```csharp
// Docker: Running containers with drasi label
InstanceCount = 5

// Process: Active drasi-server processes
InstanceCount = 3

// Kubernetes: Pods in drasi namespace
InstanceCount = 10
```

**Why nullable:** Platform might not be available or unable to query count.

---

### 2. **MaxInstances** (int?)
Maximum capacity for instances on this platform (quota/limit).

**Examples:**
```csharp
// Docker: Configuration-based limit
MaxInstances = 100

// Kubernetes: ResourceQuota limit
MaxInstances = 50

// Process: OS/hardware constraint
MaxInstances = 20
```

**Usage:**
```csharp
var utilization = (double)platform.InstanceCount / platform.MaxInstances;
if (utilization > 0.8)
    Console.WriteLine("WARNING: Platform near capacity!");
```

---

### 3. **Version** (string?)
Platform/runtime version information.

**Examples:**
```csharp
// Docker
Version = "24.0.7"

// Kubernetes
Version = "v1.28.2"

// Process (OS version or drasi-server version)
Version = "Windows 11 22H2"
```

**Use cases:**
- Compatibility checks
- Feature availability
- Upgrade planning

---

### 4. **Endpoint** (string?)
How to reach/access the platform.

**Examples:**
```csharp
// Docker
Endpoint = "unix:///var/run/docker.sock"

// Kubernetes
Endpoint = "https://k8s.example.com:6443"

// Process (local)
Endpoint = "localhost"

// Cloud provider
Endpoint = "https://draas.us-west-2.aws.amazon.com"
```

---

### 5. **Region** (string?)
Geographic location or logical grouping where platform operates.

**Examples:**
```csharp
// Cloud providers
Region = "us-west-2"
Region = "eu-central-1"

// Local/edge
Region = "on-premises"
Region = "edge-site-chicago"

// Kubernetes
Region = "us-west-1a"  // Zone
```

**Use cases:**
- Compliance (data residency)
- Latency optimization
- Cost management
- Disaster recovery

---

### 6. **Labels** (Dictionary<string, string>)
Key-value pairs for organization, filtering, and categorization.

**Common label patterns from K8s/DAPR:**
```csharp
Labels = new Dictionary<string, string>
{
    ["environment"] = "production",
    ["team"] = "platform-team",
    ["cost-center"] = "engineering",
    ["managed-by"] = "draas",
    ["tier"] = "high-availability",
    ["region"] = "us-west",
    
    // Custom organization labels
    ["department"] = "sales",
    ["project"] = "customer-portal",
    ["owner"] = "john.doe@example.com"
}
```

**Use cases:**
```csharp
// Filter platforms by environment
var prodPlatforms = platforms.Where(p => 
    p.Labels.GetValueOrDefault("environment") == "production");

// Find all platforms managed by specific team
var teamPlatforms = platforms.Where(p => 
    p.Labels.GetValueOrDefault("team") == "platform-team");

// Cost allocation
var costCenter = platform.Labels.GetValueOrDefault("cost-center");
```

---

### 7. **Metadata** (Dictionary<string, object?>)
Extensible dictionary for platform-specific properties.

**Examples by platform type:**

**Docker:**
```csharp
Metadata = new Dictionary<string, object?>
{
    ["DaemonRunning"] = true,
    ["RegistryUrl"] = "registry.example.com",
    ["NetworkMode"] = "bridge",
    ["StorageDriver"] = "overlay2",
    ["CpuCount"] = 8,
    ["MemoryTotal"] = "16GB"
}
```

**Kubernetes:**
```csharp
Metadata = new Dictionary<string, object?>
{
    ["ClusterName"] = "prod-cluster",
    ["Namespace"] = "drasi",
    ["NodeCount"] = 5,
    ["StorageClass"] = "fast-ssd",
    ["IngressClass"] = "nginx",
    ["CertManager"] = true
}
```

**Process:**
```csharp
Metadata = new Dictionary<string, object?>
{
    ["OsType"] = "Windows",
    ["Architecture"] = "x64",
    ["WorkingDirectory"] = "/var/lib/draas",
    ["ProcessOwner"] = "drasi-service",
    ["MaxMemoryMb"] = 2048
}
```

**Cloud (AWS ECS):**
```csharp
Metadata = new Dictionary<string, object?>
{
    ["ClusterArn"] = "arn:aws:ecs:us-west-2:...",
    ["VpcId"] = "vpc-12345",
    ["SubnetIds"] = new[] { "subnet-a", "subnet-b" },
    ["TaskDefinition"] = "drasi-task:5",
    ["LoadBalancerArn"] = "arn:aws:elasticloadbalancing:..."
}
```

---

## Real-World Examples

### Example 1: Docker Platform
```csharp
new Platform
{
    PlatformType = "Docker",
    IsAvailable = true,
    IsDefault = true,
    InstanceCount = 12,
    MaxInstances = 100,
    Version = "24.0.7",
    Endpoint = "unix:///var/run/docker.sock",
    Region = "on-premises",
    Labels = new()
    {
        ["environment"] = "production",
        ["managed-by"] = "draas",
        ["tier"] = "standard"
    },
    Metadata = new()
    {
        ["DaemonRunning"] = true,
        ["StorageDriver"] = "overlay2",
        ["RegistryUrl"] = "registry.internal.com"
    }
}
```

### Example 2: Kubernetes Platform
```csharp
new Platform
{
    PlatformType = "Kubernetes",
    IsAvailable = true,
    IsDefault = false,
    InstanceCount = 45,
    MaxInstances = 100,
    Version = "v1.28.2",
    Endpoint = "https://k8s-prod.example.com:6443",
    Region = "us-west-2",
    Labels = new()
    {
        ["environment"] = "production",
        ["cluster"] = "prod-main",
        ["cloud-provider"] = "aws"
    },
    Metadata = new()
    {
        ["ClusterName"] = "prod-main",
        ["Namespace"] = "drasi-system",
        ["NodeCount"] = 10,
        ["IngressClass"] = "nginx",
        ["CertManager"] = true
    }
}
```

### Example 3: AWS ECS Platform (Future)
```csharp
new Platform
{
    PlatformType = "AWS-ECS",
    IsAvailable = true,
    IsDefault = false,
    InstanceCount = 8,
    MaxInstances = 50,
    Version = "Fargate-1.4",
    Endpoint = "https://ecs.us-east-1.amazonaws.com",
    Region = "us-east-1",
    Labels = new()
    {
        ["environment"] = "staging",
        ["cloud-provider"] = "aws",
        ["cost-center"] = "engineering"
    },
    Metadata = new()
    {
        ["ClusterArn"] = "arn:aws:ecs:us-east-1:123456789:cluster/drasi",
        ["LaunchType"] = "FARGATE",
        ["NetworkMode"] = "awsvpc",
        ["CostPerHour"] = 0.12
    }
}
```

---

## Why These Properties?

### InstanceCount & MaxInstances
- **Universal concept:** Every platform tracks capacity
- **Critical for operations:** Capacity planning, scaling decisions
- **Pattern from:** Kubernetes ResourceQuotas, Docker limits, cloud quotas

### Version
- **Universal:** Every platform has a version
- **Compatibility checks:** Ensure features are supported
- **Pattern from:** K8s API versions, DAPR component versions

### Endpoint
- **Connectivity:** How to reach the platform
- **Essential for tooling:** CLIs, dashboards need to connect
- **Pattern from:** K8s kubeconfig, Docker socket, DAPR endpoint

### Region
- **Multi-location deployments:** Common in cloud/edge
- **Compliance:** Data residency requirements
- **Pattern from:** Cloud providers (AWS regions), K8s zones

### Labels
- **Flexible organization:** Not every platform needs same taxonomy
- **Filtering/grouping:** Find platforms by criteria
- **Pattern from:** K8s labels, DAPR scopes, cloud tags

### Metadata
- **Escape hatch:** Platform-specific properties without interface changes
- **Future-proof:** Add new properties without breaking changes
- **Pattern from:** All platforms use metadata/annotations extensively

---

## Guidelines for Providers

When implementing `GetPlatformsAsync()` in your provider:

1. **Always populate core fields:**
   - `PlatformType`, `IsAvailable`, `IsDefault` (required)

2. **Populate common properties when available:**
   ```csharp
   InstanceCount = GetInstanceCount(),
   MaxInstances = GetMaxCapacity(),
   Version = GetPlatformVersion(),
   Endpoint = GetConnectionEndpoint(),
   Region = GetDeploymentRegion()
   ```

3. **Use Labels for organizational metadata:**
   ```csharp
   Labels = new()
   {
       ["environment"] = configuration.Environment,
       ["managed-by"] = "draas"
   }
   ```

4. **Use Metadata for platform-specific data:**
   ```csharp
   Metadata = new()
   {
       ["SpecificToMyPlatform"] = someValue,
       ["ComplexObject"] = new { ... }
   }
   ```

---

## Anti-Patterns to Avoid

❌ **Don't put health status in Labels**
```csharp
// BAD - health is dynamic and frequent
Labels = new() { ["health"] = "healthy" }
```

✅ **Use Metadata for dynamic/frequent updates**
```csharp
// GOOD
Metadata = new() { ["HealthStatus"] = "healthy", ["LastCheck"] = DateTime.UtcNow }
```

❌ **Don't duplicate IsAvailable in multiple places**
```csharp
// BAD - redundant
IsAvailable = true,
Metadata = new() { ["Available"] = true, ["Status"] = "available" }
```

✅ **Use IsAvailable as single source of truth**
```csharp
// GOOD
IsAvailable = true,
Metadata = new() { ["LastHealthCheck"] = DateTime.UtcNow }
```

❌ **Don't put large/complex objects in Labels**
```csharp
// BAD - labels should be simple key-value
Labels = new() { ["config"] = JsonSerializer.Serialize(complexObject) }
```

✅ **Use Metadata for complex data**
```csharp
// GOOD
Metadata = new() { ["Configuration"] = complexObject }
```
