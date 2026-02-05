-- ============================================================================
-- Drasi PostgreSQL Initialization Script
-- ============================================================================
-- This script runs automatically when the PostgreSQL container is first created.
-- It sets up the replication permissions required for Drasi CDC.
--
-- NOTE: The database 'drasidb' and user 'drasi' are created automatically
-- via POSTGRES_DB and POSTGRES_USER environment variables.
-- ============================================================================

-- Grant replication permissions to the drasi user
-- Required for Drasi's Debezium-based CDC connector
ALTER USER drasi WITH REPLICATION;

-- Create a replication publication for all tables (Drasi can use this)
-- Alternatively, Drasi can create publications for specific tables
CREATE PUBLICATION drasi_publication FOR ALL TABLES;

-- ============================================================================
-- Sample Schema for Testing (Optional - Remove in production)
-- ============================================================================

-- Example: Orders table to test CDC
CREATE TABLE IF NOT EXISTS orders (
    id SERIAL PRIMARY KEY,
    customer_name VARCHAR(255) NOT NULL,
    product VARCHAR(255) NOT NULL,
    quantity INTEGER NOT NULL DEFAULT 1,
    total_amount DECIMAL(10, 2) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'pending',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Example: Customers table to test CDC
CREATE TABLE IF NOT EXISTS customers (
    id SERIAL PRIMARY KEY,
    email VARCHAR(255) UNIQUE NOT NULL,
    first_name VARCHAR(100),
    last_name VARCHAR(100),
    tier VARCHAR(20) DEFAULT 'standard',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Example: Inventory table to test CDC
CREATE TABLE IF NOT EXISTS inventory (
    id SERIAL PRIMARY KEY,
    sku VARCHAR(50) UNIQUE NOT NULL,
    product_name VARCHAR(255) NOT NULL,
    quantity_available INTEGER NOT NULL DEFAULT 0,
    reorder_threshold INTEGER NOT NULL DEFAULT 10,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Create function for auto-updating updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Apply trigger to orders table
CREATE TRIGGER update_orders_updated_at
    BEFORE UPDATE ON orders
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Apply trigger to inventory table
CREATE TRIGGER update_inventory_updated_at
    BEFORE UPDATE ON inventory
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Insert sample data for testing
INSERT INTO customers (email, first_name, last_name, tier) VALUES
    ('john.doe@example.com', 'John', 'Doe', 'premium'),
    ('jane.smith@example.com', 'Jane', 'Smith', 'standard'),
    ('bob.wilson@example.com', 'Bob', 'Wilson', 'standard');

INSERT INTO inventory (sku, product_name, quantity_available, reorder_threshold) VALUES
    ('SKU-001', 'Widget A', 100, 20),
    ('SKU-002', 'Widget B', 50, 15),
    ('SKU-003', 'Gadget X', 25, 10);

INSERT INTO orders (customer_name, product, quantity, total_amount, status) VALUES
    ('John Doe', 'Widget A', 2, 49.98, 'completed'),
    ('Jane Smith', 'Widget B', 1, 29.99, 'pending'),
    ('Bob Wilson', 'Gadget X', 3, 149.97, 'processing');

-- ============================================================================
-- Verification Queries (Run manually to verify setup)
-- ============================================================================
-- Check wal_level:           SHOW wal_level;
-- Check replication slots:   SELECT * FROM pg_replication_slots;
-- Check publications:        SELECT * FROM pg_publication;
-- Check user permissions:    \du drasi
-- ============================================================================
