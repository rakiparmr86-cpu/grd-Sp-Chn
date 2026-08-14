CREATE TABLE IF NOT EXISTS order_management_orders
(
    id CHAR(36) NOT NULL PRIMARY KEY,
    order_number VARCHAR(64) NOT NULL,
    customer_id CHAR(36) NOT NULL,
    status VARCHAR(16) NOT NULL,
    created_on_utc DATETIME(6) NOT NULL,
    updated_on_utc DATETIME(6) NOT NULL,
    CONSTRAINT uq_order_management_order_number UNIQUE (order_number),
    CONSTRAINT chk_order_management_status
        CHECK (status IN ('Pending', 'Confirmed', 'Cancelled'))
);

CREATE TABLE IF NOT EXISTS order_management_order_items
(
    order_id CHAR(36) NOT NULL,
    product_id CHAR(36) NOT NULL,
    quantity DECIMAL(18, 3) NOT NULL,
    PRIMARY KEY (order_id, product_id),
    CONSTRAINT fk_order_management_items_order
        FOREIGN KEY (order_id) REFERENCES order_management_orders (id),
    CONSTRAINT chk_order_management_item_quantity CHECK (quantity > 0)
);

CREATE TABLE IF NOT EXISTS order_management_outbox
(
    id CHAR(36) NOT NULL PRIMARY KEY,
    event_id CHAR(36) NOT NULL,
    event_type VARCHAR(255) NOT NULL,
    exchange_name VARCHAR(255) NOT NULL,
    routing_key VARCHAR(255) NOT NULL,
    payload JSON NOT NULL,
    occurred_on_utc DATETIME(6) NOT NULL,
    available_on_utc DATETIME(6) NOT NULL,
    processed_on_utc DATETIME(6) NULL,
    retry_count INT NOT NULL DEFAULT 0,
    last_error TEXT NULL,
    CONSTRAINT uq_order_management_outbox_event UNIQUE (event_id),
    INDEX ix_order_management_outbox_pending
        (processed_on_utc, available_on_utc, occurred_on_utc)
);

CREATE TABLE IF NOT EXISTS order_management_inbox
(
    event_id CHAR(36) NOT NULL PRIMARY KEY,
    event_type VARCHAR(255) NOT NULL,
    processed_on_utc DATETIME(6) NOT NULL
);

CREATE TABLE IF NOT EXISTS inventory_stock
(
    product_id CHAR(36) NOT NULL PRIMARY KEY,
    available_quantity DECIMAL(18, 3) NOT NULL,
    updated_on_utc DATETIME(6) NOT NULL,
    CONSTRAINT chk_inventory_available_quantity CHECK (available_quantity >= 0)
);

CREATE TABLE IF NOT EXISTS inventory_inbox
(
    event_id CHAR(36) NOT NULL PRIMARY KEY,
    event_type VARCHAR(255) NOT NULL,
    processed_on_utc DATETIME(6) NOT NULL
);

CREATE TABLE IF NOT EXISTS inventory_outbox
(
    id CHAR(36) NOT NULL PRIMARY KEY,
    event_id CHAR(36) NOT NULL,
    event_type VARCHAR(255) NOT NULL,
    exchange_name VARCHAR(255) NOT NULL,
    routing_key VARCHAR(255) NOT NULL,
    payload JSON NOT NULL,
    occurred_on_utc DATETIME(6) NOT NULL,
    available_on_utc DATETIME(6) NOT NULL,
    processed_on_utc DATETIME(6) NULL,
    retry_count INT NOT NULL DEFAULT 0,
    last_error TEXT NULL,
    CONSTRAINT uq_inventory_outbox_event UNIQUE (event_id),
    INDEX ix_inventory_outbox_pending
        (processed_on_utc, available_on_utc, occurred_on_utc)
);
