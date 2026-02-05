# PATCH Operations Reference

This document provides detailed examples of using PATCH operations following RFC standards for updating DRaaS instances.

## Overview

The API supports two PATCH standards:
1. **JSON Patch (RFC 6902)** - `Content-Type: application/json-patch+json`
2. **JSON Merge Patch (RFC 7396)** - `Content-Type: application/merge-patch+json`

---

## JSON Patch (RFC 6902)

### When to Use
- Precise, granular updates
- Complex transformations (copy, move)
- Conditional updates (test operation)
- Multiple operations in a single request

### Operations

#### 1. Replace Operation
Replace an existing value.

```http
PATCH /api/instances/{instanceId}
Content-Type: application/json-patch+json

[
  { "op": "replace", "path": "/configuration/logLevel", "value": "Debug" }
]
```

#### 2. Add Operation
Add a new value (or replace if exists).

```http
PATCH /api/instances/{instanceId}
Content-Type: application/json-patch+json

[
  { "op": "add", "path": "/metadata/team", "value": "platform-engineering" },
  { "op": "add", "path": "/metadata/cost-center", "value": "CC-12345" }
]
```

#### 3. Remove Operation
Remove a value.

```http
PATCH /api/instances/{instanceId}
Content-Type: application/json-patch+json

[
  { "op": "remove", "path": "/metadata/deprecated-field" }
]
```

#### 4. Test Operation
Test that a value matches before applying other operations (atomic safety).

```http
PATCH /api/instances/{instanceId}
Content-Type: application/json-patch+json

[
  { "op": "test", "path": "/configuration/logLevel", "value": "Info" },
  { "op": "replace", "path": "/configuration/logLevel", "value": "Debug" }
]
```

If the test fails, no operations are applied.

#### 5. Copy Operation
Copy a value from one location to another.

```http
PATCH /api/instances/{instanceId}
Content-Type: application/json-patch+json

[
  { "op": "copy", "from": "/metadata/original-owner", "path": "/metadata/previous-owner" }
]
```

#### 6. Move Operation
Move a value (remove from source, add to destination).

```http
PATCH /api/instances/{instanceId}
Content-Type: application/json-patch+json

[
  { "op": "move", "from": "/metadata/temp-field", "path": "/metadata/permanent-field" }
]
```

### Complex Example: Multi-Operation Update

```http
PATCH /api/instances/{instanceId}
Content-Type: application/json-patch+json

[
  {
    "op": "test",
    "path": "/configuration/logLevel",
    "value": "Info"
  },
  {
    "op": "replace",
    "path": "/configuration/logLevel",
    "value": "Debug"
  },
  {
    "op": "add",
    "path": "/metadata/updated-at",
    "value": "2024-01-15T10:30:00Z"
  },
  {
    "op": "add",
    "path": "/metadata/updated-by",
    "value": "admin@example.com"
  },
  {
    "op": "remove",
    "path": "/metadata/old-property"
  }
]
```

---

## JSON Merge Patch (RFC 7396)

### When to Use
- Simple updates
- Updating multiple fields at once
- More intuitive for most developers
- Similar to "partial PUT"

### Basic Update

```http
PATCH /api/instances/{instanceId}
Content-Type: application/merge-patch+json

{
  "configuration": {
    "logLevel": "Debug",
    "port": 9090
  }
}
```

This merges the provided fields into the existing configuration. Fields not mentioned remain unchanged.

### Update Metadata Only

```http
PATCH /api/instances/{instanceId}
Content-Type: application/merge-patch+json

{
  "metadata": {
    "environment": "production",
    "team": "platform",
    "cost-center": "CC-12345"
  }
}
```

### Remove a Field
Set a field to `null` to remove it.

```http
PATCH /api/instances/{instanceId}
Content-Type: application/merge-patch+json

{
  "metadata": {
    "deprecated-field": null
  }
}
```

### Full Configuration Update

```http
PATCH /api/instances/{instanceId}
Content-Type: application/merge-patch+json

{
  "configuration": {
    "host": "0.0.0.0",
    "port": 8080,
    "logLevel": "Debug",
    "sources": [
      {
        "kind": "PostgreSQL",
        "id": "pg-source",
        "autoStart": true
      }
    ],
    "queries": [
      {
        "id": "my-query",
        "queryText": "SELECT * FROM users WHERE active = true",
        "sources": [{ "sourceId": "pg-source" }]
      }
    ],
    "reactions": [
      {
        "kind": "Debug",
        "id": "debug-reaction",
        "queries": ["my-query"]
      }
    ]
  },
  "metadata": {
    "updated-at": "2024-01-15T10:30:00Z",
    "updated-by": "admin@example.com"
  }
}
```

---

## Choosing Between JSON Patch and Merge Patch

### Use JSON Patch When:
- ✅ You need atomic operations with test conditions
- ✅ You need to perform copy/move operations
- ✅ You want explicit control over each change
- ✅ You need to distinguish between "set to null" and "remove field"
- ✅ You're applying multiple precise transformations

### Use JSON Merge Patch When:
- ✅ You're doing simple updates
- ✅ You want more intuitive syntax
- ✅ You're sending a partial representation
- ✅ You don't need conditional updates
- ✅ You want code that's easier to read and maintain

---

## Common Scenarios

### Scenario 1: Change Log Level
**JSON Patch:**
```json
[{ "op": "replace", "path": "/configuration/logLevel", "value": "Debug" }]
```

**Merge Patch:**
```json
{ "configuration": { "logLevel": "Debug" } }
```

### Scenario 2: Add Metadata Tags
**JSON Patch:**
```json
[
  { "op": "add", "path": "/metadata/team", "value": "platform" },
  { "op": "add", "path": "/metadata/env", "value": "prod" }
]
```

**Merge Patch:**
```json
{
  "metadata": {
    "team": "platform",
    "env": "prod"
  }
}
```

### Scenario 3: Safely Update Only If Current Value Matches
**JSON Patch (only option):**
```json
[
  { "op": "test", "path": "/configuration/port", "value": 8080 },
  { "op": "replace", "path": "/configuration/port", "value": 9090 }
]
```

### Scenario 4: Reorganize Configuration
**JSON Patch (preferred):**
```json
[
  { "op": "move", "from": "/metadata/temp/setting", "path": "/configuration/setting" }
]
```

---

## Error Handling

### JSON Patch Errors
- Returns `400 Bad Request` if:
  - Path doesn't exist (for replace/remove/test)
  - Test operation fails
  - Malformed patch document
  - Invalid operation

### JSON Merge Patch Errors
- Returns `400 Bad Request` if:
  - Invalid JSON
  - Schema validation fails
  - Business rule violation (e.g., can't update while running)

---

## Best Practices

1. **Idempotency**: PATCH operations should be idempotent when possible
2. **Validation**: Always validate the patch before applying
3. **Atomic**: All operations in a JSON Patch should succeed or fail together
4. **Documentation**: Document which fields are patchable
5. **Versioning**: Consider versioning your PATCH endpoints if structure changes
6. **Testing**: Test conditional updates (JSON Patch test operation) for critical changes

---

## cURL Examples

### JSON Patch
```bash
curl -X PATCH \
  http://localhost:5000/api/instances/{instanceId} \
  -H 'Content-Type: application/json-patch+json' \
  -d '[
    { "op": "replace", "path": "/configuration/logLevel", "value": "Debug" }
  ]'
```

### JSON Merge Patch
```bash
curl -X PATCH \
  http://localhost:5000/api/instances/{instanceId} \
  -H 'Content-Type: application/merge-patch+json' \
  -d '{
    "metadata": {
      "environment": "production"
    }
  }'
```

---

## References
- [RFC 6902 - JSON Patch](https://tools.ietf.org/html/rfc6902)
- [RFC 7396 - JSON Merge Patch](https://tools.ietf.org/html/rfc7396)
- [ASP.NET Core JSON Patch](https://learn.microsoft.com/en-us/aspnet/core/web-api/jsonpatch)
