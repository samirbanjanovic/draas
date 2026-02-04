# Platform Provider Factory - Available Platforms API

## New Methods

### 1. GetAvailablePlatformTypesAsync()
Returns a simple list of available platform type names.

```csharp
var factory = serviceProvider.GetRequiredService<IPlatformInstanceProviderFactory>();
var availableTypes = await factory.GetAvailablePlatformTypesAsync(cancellationToken);

// Returns: ["Docker", "Process"] (only platforms where IsAvailable == true)
foreach (var type in availableTypes)
{
    Console.WriteLine($"Available: {type}");
}
```

### 2. GetPlatformsAsync()
Returns detailed information for all registered platforms with extensible metadata.

```csharp
var factory = serviceProvider.GetRequiredService<IPlatformInstanceProviderFactory>();
var platforms = await factory.GetPlatformsAsync(cancellationToken);

// Returns Platform records for ALL registered platforms
foreach (var platform in platforms)
{
    Console.WriteLine($"Platform: {platform.PlatformType}");
    Console.WriteLine($"  Available: {platform.IsAvailable}");
    Console.WriteLine($"  Default: {platform.IsDefault}");

    // Future: Access metadata
    if (platform.Metadata.TryGetValue("InstanceCount", out var count))
    {
        Console.WriteLine($"  Instances: {count}");
    }
}

// Example output:
// Platform: Docker
//   Available: True
//   Default: True
// Platform: Process
//   Available: True
//   Default: False
// Platform: Kubernetes
//   Available: False
//   Default: False
```

## Platform Record

```csharp
public record Platform
{
    public required string PlatformType { get; init; }     // "Docker", "Process", etc.
    public required bool IsAvailable { get; init; }        // Can be used right now
    public required bool IsDefault { get; init; }          // Is the default provider
    public Dictionary<string, object?> Metadata { get; init; } = new();  // Extensible metadata
}
```

### Future Metadata Examples

The `Metadata` dictionary allows for rich platform information:

```csharp
// Future implementation example
public Task<IEnumerable<Platform>> GetPlatformsAsync(CancellationToken ct)
{
    var platforms = _providers.Values.Select(p => new Platform
    {
        PlatformType = p.PlatformType,
        IsAvailable = p.IsAvailable,
        IsDefault = p == _defaultProvider,
        Metadata = new Dictionary<string, object?>
        {
            ["InstanceCount"] = GetInstanceCount(p),
            ["MaxInstances"] = GetMaxInstances(p),
            ["HealthStatus"] = GetHealthStatus(p),
            ["Version"] = p.Version,
            ["LastHealthCheck"] = DateTime.UtcNow
        }
    });

    return Task.FromResult(platforms);
}
```

## Usage in APIs

### REST API Example

```csharp
[ApiController]
[Route("api/platforms")]
public class PlatformsController : ControllerBase
{
    private readonly IPlatformInstanceProviderFactory _factory;

    public PlatformsController(IPlatformInstanceProviderFactory factory)
    {
        _factory = factory;
    }

    // GET /api/platforms/available
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailablePlatforms(CancellationToken ct)
    {
        var types = await _factory.GetAvailablePlatformTypesAsync(ct);
        return Ok(types);
    }

    // GET /api/platforms
    [HttpGet]
    public async Task<IActionResult> GetPlatforms(CancellationToken ct)
    {
        var platforms = await _factory.GetPlatformsAsync(ct);
        return Ok(platforms);
    }

    // GET /api/platforms/default
    [HttpGet("default")]
    public IActionResult GetDefaultPlatform()
    {
        var defaultProvider = _factory.DefaultProvider;
        return Ok(new 
        { 
            PlatformType = defaultProvider.PlatformType,
            IsAvailable = defaultProvider.IsAvailable
        });
    }
}
```

### Response Examples

**GET /api/platforms/available**
```json
[
  "Docker",
  "Process"
]
```

**GET /api/platforms**
```json
[
  {
    "platformType": "Docker",
    "isAvailable": true,
    "isDefault": true,
    "metadata": {
      "instanceCount": 5,
      "maxInstances": 100,
      "healthStatus": "Healthy"
    }
  },
  {
    "platformType": "Process",
    "isAvailable": true,
    "isDefault": false,
    "metadata": {
      "instanceCount": 2,
      "maxInstances": 50,
      "healthStatus": "Healthy"
    }
  },
  {
    "platformType": "Kubernetes",
    "isAvailable": false,
    "isDefault": false,
    "metadata": {
      "healthStatus": "Unavailable",
      "reason": "kubectl not configured"
    }
  }
]
```

**GET /api/platforms/default**
```json
{
  "platformType": "Docker",
  "isAvailable": true
}
```

## Use Cases

### 1. CLI Tool - Show Available Platforms
```csharp
var factory = serviceProvider.GetRequiredService<IPlatformInstanceProviderFactory>();
var types = await factory.GetAvailablePlatformTypesAsync(default);

Console.WriteLine("Available platforms:");
foreach (var type in types)
{
    Console.WriteLine($"  - {type}");
}
```

### 2. UI - Dropdown List
```csharp
// Populate dropdown with only available platforms
var availableTypes = await _factory.GetAvailablePlatformTypesAsync(default);
platformDropdown.DataSource = availableTypes.ToList();
```

### 3. Validation - Check if Platform Exists
```csharp
var requestedPlatform = "Kubernetes";
var platforms = await _factory.GetPlatformsAsync(default);

var platform = platforms.FirstOrDefault(p => 
    p.PlatformType.Equals(requestedPlatform, StringComparison.OrdinalIgnoreCase));

if (platform == null)
{
    return BadRequest($"Platform '{requestedPlatform}' is not registered");
}

if (!platform.IsAvailable)
{
    return BadRequest($"Platform '{requestedPlatform}' is registered but not available");
}
```

### 4. Dashboard - Platform Status with Metadata
```csharp
var platforms = await _factory.GetPlatformsAsync(default);

foreach (var platform in platforms)
{
    var status = platform.IsDefault ? "Default" : 
                 platform.IsAvailable ? "Available" : "Unavailable";

    Console.WriteLine($"{platform.PlatformType}: {status}");

    // Display metadata if available
    if (platform.Metadata.TryGetValue("InstanceCount", out var count))
    {
        Console.WriteLine($"  Instances: {count}");
    }

    if (platform.Metadata.TryGetValue("HealthStatus", out var health))
    {
        Console.WriteLine($"  Health: {health}");
    }
}
```

### 5. Monitoring - Track Platform Capacity
```csharp
var platforms = await _factory.GetPlatformsAsync(default);

foreach (var platform in platforms.Where(p => p.IsAvailable))
{
    var current = platform.Metadata.GetValueOrDefault("InstanceCount", 0);
    var max = platform.Metadata.GetValueOrDefault("MaxInstances", int.MaxValue);
    var utilization = (double)current / (int)max * 100;

    Console.WriteLine($"{platform.PlatformType}: {current}/{max} ({utilization:F1}%)");

    if (utilization > 80)
    {
        Console.WriteLine($"  WARNING: {platform.PlatformType} nearing capacity!");
    }
}
```

## Thread Safety

All methods are thread-safe:
- Factory is a singleton
- All data is immutable after construction
- No mutable state

```csharp
// Safe to call from multiple threads
await Task.WhenAll(
    _factory.GetAvailablePlatformTypesAsync(ct),
    _factory.GetPlatformsAsync(ct),
    _factory.GetPlatformsAsync(ct)
);
```

## Extending with Metadata

When implementing providers, you can add custom metadata:

```csharp
public class DockerInstanceProvider : IPlatformInstanceProvider
{
    private readonly ConcurrentDictionary<string, DrasiInstance> _instances = new();

    public string PlatformType => "Docker";
    public bool IsAvailable => CheckDockerAvailable();

    // Factory can query this for metadata
    public int GetInstanceCount() => _instances.Count;
    public int GetMaxInstances() => 100; // Configuration-based
    public string GetHealthStatus() => IsAvailable ? "Healthy" : "Unavailable";
}
```
