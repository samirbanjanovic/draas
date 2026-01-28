# DRaaS - Drasi as a Service

A custom control plane API for configuring and managing instances of [Drasi](https://drasi.io/) servers. DRaaS provides a centralized management and access layer for Drasi instances, enabling teams to interact with Drasi without direct exposure to the underlying infrastructure.

## Overview

DRaaS (Drasi as a Service) serves as a **control plane** that sits between teams/consumers and isolated Drasi server instances. It provides:

- **Centralized Configuration Management**: Generate and manage `config.yaml` files for Drasi servers
- **Abstracted Access Layer**: Teams interact with Drasi through this API rather than directly accessing Drasi instances
- **Configuration CRUD Operations**: REST API endpoints for retrieving and patching Drasi server configurations

### Architecture Context

In the broader architecture, the Control Plane acts as a gateway that:
1. Receives configuration requests from teams via REST API
2. Manages Drasi server configurations (Sources, Queries, Reactions)
3. Provides isolation between consumers and Drasi instances

## Current Features

### Configuration API

The API currently exposes endpoints to manage Drasi server configurations:

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/Configuration` | GET | Retrieve the current Drasi server configuration |
| `/api/Configuration` | PATCH | Apply JSON Patch updates to the configuration |

### Configuration Model

The configuration follows the Drasi server configuration structure:

```yaml
sources:
  - kind: <source-type>
    id: <source-id>
    autoStart: true/false

queries:
  - id: <query-id>
    queryText: "<cypher-query>"
    sources:
      - sourceId: <source-id>

reactions:
  - kind: <reaction-type>
    id: <reaction-id>
    queries: [<query-ids>]
```

**Components:**
- **Sources**: Define data sources that Drasi connects to (kind, id, autoStart)
- **Queries**: Define Cypher queries that process data from sources
- **Reactions**: Define outputs/actions triggered by query results

## Technology Stack

- **.NET 10.0** - ASP.NET Core Web API
- **YamlDotNet** - YAML serialization/deserialization for Drasi config files
- **JSON Patch (RFC 6902)** - Partial configuration updates via `Microsoft.AspNetCore.JsonPatch`
- **Scalar** - OpenAPI documentation UI (available in development mode)

## Getting Started

### Prerequisites

- .NET 10.0 SDK

### Running the Application

```bash
cd src/DRaaS.ControlPlane
dotnet run
```

### API Documentation

When running in development mode, the API documentation is available via Scalar at the `/scalar/v1` endpoint.

## Project Structure

```
src/DRaaS.ControlPlane/
├── Controllers/
│   └── ConfigurationController.cs    # REST API endpoints
├── Models/
│   ├── Configuration.cs              # Root configuration model
│   ├── Source.cs                     # Data source definition
│   ├── Query.cs                      # Query definition
│   ├── QuerySource.cs                # Query-to-source mapping
│   └── Reaction.cs                   # Reaction definition
├── Providers/
│   ├── IDrasiServerConfigurationProvider.cs   # Configuration provider interface
│   └── DrasiServerConfigurationProvider.cs    # YAML-based configuration provider
└── Program.cs                        # Application entry point
```

## Planned Features

The following capabilities are planned for future development:

- **RBAC (Role-Based Access Control)**: Control which users/teams can access specific configurations
- **Query-to-RBAC Matching**: Determine if queries match RBAC roles and permissions
- **Persistent Storage**: Configuration persistence to file or external storage
- **Multi-Instance Management**: Support for managing multiple Drasi server instances

## Development Status

⚠️ **This project is under active development.**

Current implementation includes:
- ✅ Configuration model structure (Sources, Queries, Reactions)
- ✅ GET endpoint for retrieving configuration
- ✅ PATCH endpoint for updating configuration (JSON Patch)
- ✅ YAML serialization/deserialization
- 🔲 Persistent storage (currently uses in-memory/hardcoded data)
- 🔲 RBAC implementation
- 🔲 Multi-instance Drasi management
