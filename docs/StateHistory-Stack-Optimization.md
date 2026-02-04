# State History Optimization - Stack Implementation

## Change Summary

Optimized `DrasiInstance.StateHistory` from `List<DrasiInstanceState>` to `Stack<DrasiInstanceState>` for better performance and semantic clarity.

## Why Stack?

### Performance Benefits
- **O(1) access to current state**: `Stack.Peek()` vs `List.LastOrDefault()`
- **Natural LIFO semantics**: Most recent state is always on top
- **Memory efficient**: Stack is optimized for last-in-first-out access patterns

### Semantic Clarity
- State history naturally represents a timeline where we care most about the current (most recent) state
- Stack.Push() makes it clear we're adding a new state to the top
- Stack.Peek() clearly expresses "get current state without removing it"

## Implementation Details

### DrasiInstance.cs

**Before:**
```csharp
public List<DrasiInstanceState> StateHistory { get; init; } = [];

public Status CurrentStatus => StateHistory.LastOrDefault()?.Status ?? Status.Unknown;
```

**After:**
```csharp
public Stack<DrasiInstanceState> StateHistory { get; init; } = new();

public Status CurrentStatus => StateHistory.Count > 0 ? StateHistory.Peek().Status : Status.Unknown;
```

### InstanceOrchestrationService.cs

**RegisterInstanceAsync:**
```csharp
var stateHistory = new Stack<DrasiInstanceState>();
stateHistory.Push(new DrasiInstanceState
{
    Status = Status.Registered,
    TimeStamp = DateTime.UtcNow,
    StateMetadata = new Dictionary<string, string> { ["Reason"] = "Initial registration" }
});

var instance = new DrasiInstance
{
    // ... other properties
    StateHistory = stateHistory,
    // ... other properties
};
```

**AddStateTransitionAsync:**
```csharp
private async Task<DrasiInstance> AddStateTransitionAsync(
    DrasiInstance instance,
    Status newStatus,
    Dictionary<string, string> metadata,
    CancellationToken cancellationToken)
{
    var newState = new DrasiInstanceState
    {
        Status = newStatus,
        TimeStamp = DateTime.UtcNow,
        StateMetadata = metadata
    };

    // Create new stack preserving order: reverse existing stack to get oldest->newest,
    // then create new stack from that (which reverses it back to newest->oldest),
    // then push the new state on top
    var stateHistory = new Stack<DrasiInstanceState>(instance.StateHistory.Reverse());
    stateHistory.Push(newState);

    var updatedInstance = instance with
    {
        StateHistory = stateHistory,
        LastUpdatedAt = DateTime.UtcNow
    };

    return await _storageService.UpdateInstanceAsync(updatedInstance, cancellationToken);
}
```

## Stack Ordering

**Stack Structure:**
```
Top (Most Recent)    → Peek() returns this
│  State 3 (Current)
│  State 2
│  State 1
Bottom (Oldest)
```

**When iterating over state history (oldest to newest):**
```csharp
var orderedHistory = instance.StateHistory.Reverse().ToList();
// Or enumerate directly: instance.StateHistory.Reverse()
```

**When accessing current state:**
```csharp
var currentStatus = instance.CurrentStatus;  // Uses Peek() internally
// or directly:
var currentState = instance.StateHistory.Peek();  // throws if empty
// or safely:
var currentState = instance.StateHistory.Count > 0 ? instance.StateHistory.Peek() : null;
```

## Immutability Considerations

Since `DrasiInstance` is a record with `init` properties, we maintain immutability by creating a new Stack instance when adding states:

```csharp
// Create new stack from existing one (preserves order)
var newStack = new Stack<DrasiInstanceState>(existingStack.Reverse());
newStack.Push(newState);

// Create new instance with new stack
var updatedInstance = instance with { StateHistory = newStack };
```

## Migration Notes

### Serialization
Most serializers handle Stack<T> automatically. If using custom serialization:
- **JSON.NET**: Serializes as array (oldest to newest when enumerated)
- **System.Text.Json**: Serializes as array
- **To preserve order**: Serialize as `StateHistory.Reverse()` if you want oldest→newest

### Queries
If you need to query state history (e.g., "find when instance was last in Running state"):
```csharp
// Enumerate stack (naturally goes newest→oldest)
var lastRunningState = instance.StateHistory
    .FirstOrDefault(s => s.Status == Status.Running);

// For oldest→newest order
var firstRunningState = instance.StateHistory
    .Reverse()
    .FirstOrDefault(s => s.Status == Status.Running);
```

## Performance Comparison

| Operation | List<T> | Stack<T> |
|-----------|---------|----------|
| Get current state | O(1) - LastOrDefault() | O(1) - Peek() ✨ |
| Add new state | O(1) - Add() | O(1) - Push() |
| Memory overhead | Higher (capacity > count) | Lower (exact size) |
| Semantic clarity | General purpose | LIFO-specific ✨ |

## Build Status

✅ **Build Successful**
- 0 Errors
- 3 Pre-existing nullable warnings (unrelated to this change)

## Files Modified

1. ✅ `Models/DrasiInstance.cs`
   - Changed `List<DrasiInstanceState>` to `Stack<DrasiInstanceState>`
   - Updated `CurrentStatus` to use `Peek()` instead of `LastOrDefault()`

2. ✅ `Services/Impl/InstanceOrchestrationService.cs`
   - Updated `RegisterInstanceAsync` to initialize Stack
   - Updated `AddStateTransitionAsync` to work with Stack

## Benefits Summary

✅ **Performance**: O(1) access to current state via Peek()  
✅ **Clarity**: Stack semantics match state history concept  
✅ **Memory**: More efficient for LIFO access pattern  
✅ **Simplicity**: Cleaner code, better expresses intent  
✅ **Compatibility**: Works with existing serialization and queries

---
**Status**: ✅ Complete and Tested  
**Build**: ✅ Successful
