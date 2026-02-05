# Drasi Infrastructure Mocking Context

## Overview

This document provides context for mocking infrastructure components for local development and testing with Microsoft's **Drasi** platform - a Data Change Processing platform that simplifies detecting changes in data and taking immediate action.

**Official Resources:**
- Documentation: https://drasi.io
- GitHub: https://github.com/drasi-project/drasi-platform
- Discord: https://aka.ms/drasidiscord

---

## What is Drasi?

Drasi is a CNCF Sandbox project that provides real-time actionable insights without the overhead of traditional data processing methods. It tracks system changes and events without needing to copy data to a central data lake or repeatedly query data sources.

### Core Components

1. **Sources** - Connect to data repositories to monitor logs and feeds for changing data
2. **Continuous Queries** - Written in Cypher Query Language, continuously evaluate incoming data changes
3. **Reactions** - Trigger meaningful responses based on query result updates

---

## Supported Database Sources

Drasi supports the following relational databases as sources:

| Database | CDC Method | Configuration Requirements |
|----------|------------|----------------------------|
| **PostgreSQL** | Logical Replication (WAL) | `wal_level = logical`, REPLICATION + LOGIN roles |
| **MySQL** | Binary Log (binlog) | `binlog_format = ROW`, REPLICATION SLAVE/CLIENT |
| **Microsoft SQL Server** | Change Data Capture | CDC enabled on tables |

### Additional Sources
- Azure Cosmos DB (Gremlin API)
- Kubernetes
- Microsoft Dataverse
- Azure EventHub

---

## PostgreSQL Source Requirements

### Database Configuration
```
wal_level = logical
max_wal_senders = 10
max_replication_slots = 10
```

### User Permissions
- `REPLICATION` - Required for logical replication
- `LOGIN` - Required for database connection
- `SELECT` - Required on tables to capture
- `CREATE` - Optional, allows Drasi to create publications

### Drasi Source Definition Example
```yaml
apiVersion: v1
kind: Source
name: local-postgres
spec:
  kind: PostgreSQL
  properties:
    host: localhost
    port: 5432
    user: drasi
    password: drasi_password
    database: drasidb
    ssl: false
    tables:
      - public.your_table
```

---

## MySQL Source Requirements

### Database Configuration
```
server-id = 223344
log_bin = mysql-bin
binlog_format = ROW
binlog_row_image = FULL
binlog_expire_logs_seconds = 864000
```

### User Permissions
- `REPLICATION SLAVE` - Connect to and read binlog
- `REPLICATION CLIENT` - Execute status commands
- `SELECT` - Required on tables to capture

### Drasi Source Definition Example
```yaml
apiVersion: v1
kind: Source
name: local-mysql
spec:
  kind: MySQL
  properties:
    host: localhost
    port: 3306
    user: drasi
    password: drasi_password
    database: drasidb
    tables:
      - drasidb.your_table
```

---

## Local Development Setup

### Directory Structure
```
mock/
├── psql-source/          # PostgreSQL for Drasi testing
│   └── docker-compose.yml
├── graphdb-source/       # Graph database mocking
│   └── docker-compose.yml
```

### Running Local Databases
```bash
# Start PostgreSQL for Drasi
cd mock/psql-source
docker-compose up -d

# Stop and remove
docker-compose down

# Stop but preserve data
docker-compose stop
```

### Connecting Drasi to Local Database
When Drasi runs in Kubernetes (e.g., Docker Desktop K8s, minikube):
- Use `host.docker.internal` for host machine access
- Or use the container's network IP

---

## Data Model Translation

Drasi translates relational data to property graph format:
- Each **table row** becomes a **graph node**
- **Columns** become **node properties**
- **Node ID** = composite of table name + primary key
- **Node Label** = table name

Foreign keys are NOT automatically translated to edges. Use Drasi's **Source Join** feature in Continuous Queries instead.

---

## Best Practices

1. **Use PostgreSQL for simpler CDC setup** - Logical replication is easier to configure
2. **Enable persistence** - Use Docker volumes for data survival across restarts
3. **Use dedicated CDC user** - Don't use superuser for replication
4. **Configure sufficient WAL retention** - Ensure enough history for Drasi to catch up
5. **Test CDC before Drasi integration** - Verify replication works independently first

---

## Troubleshooting

### PostgreSQL
- Verify `wal_level`: `SHOW wal_level;` → should return `logical`
- Check replication slots: `SELECT * FROM pg_replication_slots;`
- Verify user permissions: `\du` in psql

### MySQL
- Verify binlog: `SHOW VARIABLES LIKE 'log_bin';` → should return `ON`
- Check binlog format: `SHOW VARIABLES LIKE 'binlog_format';` → should return `ROW`

---

## References

- [Drasi PostgreSQL Source Setup](https://drasi.io/how-to-guides/configure-sources/configure-postgresql-source/setup-postgresql-replication/)
- [Drasi MySQL Source Setup](https://drasi.io/how-to-guides/configure-sources/configure-mysql-source/setup-mysql/)
- [Debezium PostgreSQL Connector](https://debezium.io/documentation/reference/stable/connectors/postgresql.html)
- [Debezium MySQL Connector](https://debezium.io/documentation/reference/2.7/connectors/mysql.html)
