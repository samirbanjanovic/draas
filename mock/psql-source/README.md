# PostgreSQL Source for Drasi Testing

This directory contains a Docker Compose setup for running a local PostgreSQL database configured for **Drasi Change Data Capture (CDC)**.

## Quick Start

```bash
# Start PostgreSQL
docker-compose up -d

# Check status
docker-compose ps

# View logs
docker-compose logs -f postgres

# Stop (preserves data)
docker-compose stop

# Stop and remove containers (preserves data in volume)
docker-compose down

# Stop and remove everything including data
docker-compose down -v
```

## Connection Details

| Property | Value |
|----------|-------|
| **Host** | `localhost` |
| **Port** | `5432` |
| **Database** | `drasidb` |
| **Username** | `drasi` |
| **Password** | `drasi_password` |

### From Docker/Kubernetes

If connecting from another container or from Kubernetes on Docker Desktop:
- Use `host.docker.internal:5432` instead of `localhost`
- Or use the container name `drasi-postgres-source` if on the same Docker network

## Drasi Source Configuration

Create this YAML file to configure Drasi to connect to this PostgreSQL instance:

```yaml
apiVersion: v1
kind: Source
name: local-postgres
spec:
  kind: PostgreSQL
  properties:
    host: host.docker.internal  # or localhost if Drasi runs on host
    port: 5432
    user: drasi
    password: drasi_password
    database: drasidb
    ssl: false
    tables:
      - public.orders
      - public.customers
      - public.inventory
```

Apply with:
```bash
drasi apply -f postgres-source.yaml
```

## Included Sample Tables

The init script creates these tables for testing:

### `orders`
| Column | Type | Description |
|--------|------|-------------|
| id | SERIAL | Primary key |
| customer_name | VARCHAR(255) | Customer name |
| product | VARCHAR(255) | Product name |
| quantity | INTEGER | Order quantity |
| total_amount | DECIMAL | Order total |
| status | VARCHAR(50) | Order status |
| created_at | TIMESTAMP | Creation time |
| updated_at | TIMESTAMP | Last update time |

### `customers`
| Column | Type | Description |
|--------|------|-------------|
| id | SERIAL | Primary key |
| email | VARCHAR(255) | Unique email |
| first_name | VARCHAR(100) | First name |
| last_name | VARCHAR(100) | Last name |
| tier | VARCHAR(20) | Customer tier |
| created_at | TIMESTAMP | Creation time |

### `inventory`
| Column | Type | Description |
|--------|------|-------------|
| id | SERIAL | Primary key |
| sku | VARCHAR(50) | Unique SKU |
| product_name | VARCHAR(255) | Product name |
| quantity_available | INTEGER | Stock quantity |
| reorder_threshold | INTEGER | Reorder point |
| updated_at | TIMESTAMP | Last update time |

## Testing CDC

Connect to the database and make changes:

```bash
# Connect via psql
docker exec -it drasi-postgres-source psql -U drasi -d drasidb

# Or use any PostgreSQL client with the connection details above
```

Example test queries:

```sql
-- Insert a new order (should trigger Drasi)
INSERT INTO orders (customer_name, product, quantity, total_amount, status)
VALUES ('New Customer', 'Test Product', 1, 99.99, 'pending');

-- Update an order status (should trigger Drasi)
UPDATE orders SET status = 'shipped' WHERE id = 1;

-- Update inventory (should trigger Drasi)
UPDATE inventory SET quantity_available = quantity_available - 5 WHERE sku = 'SKU-001';
```

## Verify Replication Setup

```sql
-- Check wal_level is set to 'logical'
SHOW wal_level;

-- List publications
SELECT * FROM pg_publication;

-- Check replication slots (will be empty until Drasi connects)
SELECT * FROM pg_replication_slots;

-- Verify user has replication permission
SELECT rolname, rolreplication FROM pg_roles WHERE rolname = 'drasi';
```

## Optional: pgAdmin

Start with pgAdmin for a web-based database management UI:

```bash
docker-compose --profile tools up -d
```

Access at: http://localhost:5050
- Email: `admin@drasi.local`
- Password: `admin`

## Persistence

Data is persisted in the `drasi-postgres-data` Docker volume. The database survives:
- Container restarts
- `docker-compose down`
- `docker-compose stop`

To completely reset the database:
```bash
docker-compose down -v
docker-compose up -d
```

## Troubleshooting

### Cannot connect to database
```bash
# Check if container is running
docker-compose ps

# Check container logs
docker-compose logs postgres

# Verify port is exposed
docker port drasi-postgres-source
```

### Drasi cannot detect changes
1. Verify `wal_level` is `logical`: `SHOW wal_level;`
2. Check the publication exists: `SELECT * FROM pg_publication;`
3. Verify user has REPLICATION role: `\du drasi`
4. Check Drasi source status: `drasi list source`

### Permission denied errors
The `drasi` user should have all necessary permissions. If issues persist:
```sql
-- Grant all privileges (for testing only)
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO drasi;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO drasi;
```
