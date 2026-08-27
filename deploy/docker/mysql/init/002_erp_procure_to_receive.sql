-- First ERP vertical slice: hierarchy + login + material request + PO + GRN + location stock.
-- This file is idempotent and is automatically applied only to a fresh MySQL volume.

CREATE TABLE IF NOT EXISTS organization_units
(
    id CHAR(36) NOT NULL PRIMARY KEY,
    parent_id CHAR(36) NULL,
    code VARCHAR(32) NOT NULL,
    name VARCHAR(160) NOT NULL,
    unit_type VARCHAR(32) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_on_utc DATETIME(6) NOT NULL,
    CONSTRAINT uq_organization_unit_code UNIQUE (code),
    CONSTRAINT fk_organization_unit_parent
        FOREIGN KEY (parent_id) REFERENCES organization_units (id),
    INDEX ix_organization_unit_parent (parent_id)
);

INSERT IGNORE INTO organization_units
    (id, parent_id, code, name, unit_type, is_active, created_on_utc)
VALUES
    ('00000000-0000-0000-0000-000000000001', NULL, 'GRD', 'GRD Enterprise', 'Enterprise', TRUE, UTC_TIMESTAMP(6)),
    ('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', 'HO', 'Head Office', 'HeadOffice', TRUE, UTC_TIMESTAMP(6)),
    ('00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000002', 'NORTH', 'Regional Office North', 'RegionalOffice', TRUE, UTC_TIMESTAMP(6)),
    ('00000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000003', 'DELHI-HB', 'Delhi Head Branch', 'HeadBranch', TRUE, UTC_TIMESTAMP(6)),
    ('00000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000004', 'DELHI-BR', 'Delhi Branch', 'Branch', TRUE, UTC_TIMESTAMP(6)),
    ('00000000-0000-0000-0000-000000000006', '00000000-0000-0000-0000-000000000005', 'DELHI-PLANT', 'Delhi Manufacturing Plant', 'ManufacturingPlant', TRUE, UTC_TIMESTAMP(6)),
    ('00000000-0000-0000-0000-000000000007', '00000000-0000-0000-0000-000000000005', 'DELHI-WH', 'Delhi Warehouse', 'Warehouse', TRUE, UTC_TIMESTAMP(6)),
    ('00000000-0000-0000-0000-000000000008', '00000000-0000-0000-0000-000000000005', 'DELHI-BOILER', 'Delhi Boiler Consumption Unit', 'ConsumptionUnit', TRUE, UTC_TIMESTAMP(6)),
    ('00000000-0000-0000-0000-000000000009', '00000000-0000-0000-0000-000000000005', 'DELHI-SALES', 'Delhi Sales Branch', 'SalesBranch', TRUE, UTC_TIMESTAMP(6));

CREATE TABLE IF NOT EXISTS identity_users
(
    id CHAR(36) NOT NULL PRIMARY KEY,
    user_name VARCHAR(160) NOT NULL,
    normalized_user_name VARCHAR(160) NOT NULL,
    password_hash VARCHAR(512) NOT NULL,
    role_name VARCHAR(64) NOT NULL,
    organization_unit_id CHAR(36) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_on_utc DATETIME(6) NOT NULL,
    CONSTRAINT uq_identity_normalized_user_name UNIQUE (normalized_user_name),
    INDEX ix_identity_user_organization (organization_unit_id)
);

CREATE TABLE IF NOT EXISTS identity_user_permissions
(
    user_id CHAR(36) NOT NULL,
    permission_code VARCHAR(128) NOT NULL,
    PRIMARY KEY (user_id, permission_code),
    CONSTRAINT fk_identity_permissions_user
        FOREIGN KEY (user_id) REFERENCES identity_users (id)
);

-- Local demonstration accounts only. Every account uses password 1223456.
-- Replace these records and Jwt:SigningKey before any shared or production deployment.
INSERT IGNORE INTO identity_users
    (id, user_name, normalized_user_name, password_hash, role_name,
     organization_unit_id, is_active, created_on_utc)
VALUES
    ('10000000-0000-0000-0000-000000000001', 'director@grd.local', 'DIRECTOR@GRD.LOCAL', 'pbkdf2-sha256$100000$R1JELWxvY2FsLXNlZWQtc2FsdC0yMDI2$mEAf2fEzJ6I2K6etT9SQ8VbHO0Ty7qnoiSiR1KpuE8c=', 'Director', '00000000-0000-0000-0000-000000000002', TRUE, UTC_TIMESTAMP(6)),
    ('10000000-0000-0000-0000-000000000002', 'gm.north@grd.local', 'GM.NORTH@GRD.LOCAL', 'pbkdf2-sha256$100000$R1JELWxvY2FsLXNlZWQtc2FsdC0yMDI2$mEAf2fEzJ6I2K6etT9SQ8VbHO0Ty7qnoiSiR1KpuE8c=', 'GeneralManager', '00000000-0000-0000-0000-000000000003', TRUE, UTC_TIMESTAMP(6)),
    ('10000000-0000-0000-0000-000000000003', 'manager.purchase@grd.local', 'MANAGER.PURCHASE@GRD.LOCAL', 'pbkdf2-sha256$100000$R1JELWxvY2FsLXNlZWQtc2FsdC0yMDI2$mEAf2fEzJ6I2K6etT9SQ8VbHO0Ty7qnoiSiR1KpuE8c=', 'Manager', '00000000-0000-0000-0000-000000000004', TRUE, UTC_TIMESTAMP(6)),
    ('10000000-0000-0000-0000-000000000004', 'supervisor.plant@grd.local', 'SUPERVISOR.PLANT@GRD.LOCAL', 'pbkdf2-sha256$100000$R1JELWxvY2FsLXNlZWQtc2FsdC0yMDI2$mEAf2fEzJ6I2K6etT9SQ8VbHO0Ty7qnoiSiR1KpuE8c=', 'Supervisor', '00000000-0000-0000-0000-000000000006', TRUE, UTC_TIMESTAMP(6)),
    ('10000000-0000-0000-0000-000000000005', 'executive.boiler@grd.local', 'EXECUTIVE.BOILER@GRD.LOCAL', 'pbkdf2-sha256$100000$R1JELWxvY2FsLXNlZWQtc2FsdC0yMDI2$mEAf2fEzJ6I2K6etT9SQ8VbHO0Ty7qnoiSiR1KpuE8c=', 'Executive', '00000000-0000-0000-0000-000000000008', TRUE, UTC_TIMESTAMP(6)),
    ('10000000-0000-0000-0000-000000000006', 'manager.hr@grd.local', 'MANAGER.HR@GRD.LOCAL', 'pbkdf2-sha256$100000$R1JELWxvY2FsLXNlZWQtc2FsdC0yMDI2$mEAf2fEzJ6I2K6etT9SQ8VbHO0Ty7qnoiSiR1KpuE8c=', 'Manager', '00000000-0000-0000-0000-000000000002', TRUE, UTC_TIMESTAMP(6));

INSERT IGNORE INTO identity_user_permissions (user_id, permission_code)
VALUES
    ('10000000-0000-0000-0000-000000000001', 'identity.user.create'),
    ('10000000-0000-0000-0000-000000000001', 'organization.read'),
    ('10000000-0000-0000-0000-000000000001', 'organization.manage'),
    ('10000000-0000-0000-0000-000000000001', 'procurement.material-request.create'),
    ('10000000-0000-0000-0000-000000000001', 'procurement.material-request.read'),
    ('10000000-0000-0000-0000-000000000001', 'procurement.material-request.approve'),
    ('10000000-0000-0000-0000-000000000001', 'procurement.purchase-order.create'),
    ('10000000-0000-0000-0000-000000000001', 'procurement.purchase-order.read'),
    ('10000000-0000-0000-0000-000000000001', 'inventory.stock.read'),
    ('10000000-0000-0000-0000-000000000001', 'warehouse.goods-receipt.read'),
    ('10000000-0000-0000-0000-000000000001', 'warehouse.goods-receipt.post'),
    ('10000000-0000-0000-0000-000000000002', 'organization.read'),
    ('10000000-0000-0000-0000-000000000002', 'procurement.material-request.read'),
    ('10000000-0000-0000-0000-000000000002', 'procurement.material-request.approve'),
    ('10000000-0000-0000-0000-000000000002', 'procurement.purchase-order.create'),
    ('10000000-0000-0000-0000-000000000002', 'procurement.purchase-order.read'),
    ('10000000-0000-0000-0000-000000000002', 'inventory.stock.read'),
    ('10000000-0000-0000-0000-000000000003', 'organization.read'),
    ('10000000-0000-0000-0000-000000000003', 'procurement.material-request.read'),
    ('10000000-0000-0000-0000-000000000003', 'procurement.material-request.approve'),
    ('10000000-0000-0000-0000-000000000003', 'procurement.purchase-order.create'),
    ('10000000-0000-0000-0000-000000000003', 'procurement.purchase-order.read'),
    ('10000000-0000-0000-0000-000000000003', 'inventory.stock.read'),
    ('10000000-0000-0000-0000-000000000004', 'organization.read'),
    ('10000000-0000-0000-0000-000000000004', 'procurement.material-request.create'),
    ('10000000-0000-0000-0000-000000000004', 'procurement.material-request.read'),
    ('10000000-0000-0000-0000-000000000004', 'inventory.stock.read'),
    ('10000000-0000-0000-0000-000000000004', 'warehouse.goods-receipt.read'),
    ('10000000-0000-0000-0000-000000000004', 'warehouse.goods-receipt.post'),
    ('10000000-0000-0000-0000-000000000005', 'organization.read'),
    ('10000000-0000-0000-0000-000000000005', 'procurement.material-request.create'),
    ('10000000-0000-0000-0000-000000000005', 'procurement.material-request.read'),
    ('10000000-0000-0000-0000-000000000006', 'organization.read'),
    ('10000000-0000-0000-0000-000000000006', 'identity.user.create');

CREATE TABLE IF NOT EXISTS procurement_material_requests
(
    id CHAR(36) NOT NULL PRIMARY KEY,
    request_number VARCHAR(64) NOT NULL,
    requesting_organization_unit_id CHAR(36) NOT NULL,
    destination_organization_unit_id CHAR(36) NOT NULL,
    requested_by_user_id CHAR(36) NOT NULL,
    purpose VARCHAR(500) NOT NULL,
    status VARCHAR(32) NOT NULL,
    approved_by_user_id CHAR(36) NULL,
    purchase_order_id CHAR(36) NULL,
    created_on_utc DATETIME(6) NOT NULL,
    updated_on_utc DATETIME(6) NOT NULL,
    CONSTRAINT uq_procurement_request_number UNIQUE (request_number),
    INDEX ix_procurement_request_org_status (requesting_organization_unit_id, status)
);

CREATE TABLE IF NOT EXISTS procurement_material_request_items
(
    material_request_id CHAR(36) NOT NULL,
    product_id CHAR(36) NOT NULL,
    quantity DECIMAL(18, 3) NOT NULL,
    unit_of_measure VARCHAR(16) NOT NULL,
    PRIMARY KEY (material_request_id, product_id),
    CONSTRAINT fk_procurement_request_item
        FOREIGN KEY (material_request_id) REFERENCES procurement_material_requests (id),
    CONSTRAINT chk_procurement_request_quantity CHECK (quantity > 0)
);

CREATE TABLE IF NOT EXISTS procurement_purchase_orders
(
    id CHAR(36) NOT NULL PRIMARY KEY,
    purchase_order_number VARCHAR(64) NOT NULL,
    material_request_id CHAR(36) NOT NULL,
    supplier_id CHAR(36) NOT NULL,
    destination_organization_unit_id CHAR(36) NOT NULL,
    currency CHAR(3) NOT NULL,
    status VARCHAR(32) NOT NULL,
    issued_on_utc DATETIME(6) NOT NULL,
    updated_on_utc DATETIME(6) NOT NULL,
    CONSTRAINT uq_procurement_po_number UNIQUE (purchase_order_number),
    CONSTRAINT uq_procurement_po_request UNIQUE (material_request_id),
    CONSTRAINT fk_procurement_po_request
        FOREIGN KEY (material_request_id) REFERENCES procurement_material_requests (id),
    INDEX ix_procurement_po_destination_status (destination_organization_unit_id, status)
);

CREATE TABLE IF NOT EXISTS procurement_purchase_order_items
(
    purchase_order_id CHAR(36) NOT NULL,
    product_id CHAR(36) NOT NULL,
    quantity DECIMAL(18, 3) NOT NULL,
    unit_of_measure VARCHAR(16) NOT NULL,
    unit_price DECIMAL(18, 4) NOT NULL,
    PRIMARY KEY (purchase_order_id, product_id),
    CONSTRAINT fk_procurement_po_item
        FOREIGN KEY (purchase_order_id) REFERENCES procurement_purchase_orders (id),
    CONSTRAINT chk_procurement_po_quantity CHECK (quantity > 0),
    CONSTRAINT chk_procurement_po_price CHECK (unit_price > 0)
);

CREATE TABLE IF NOT EXISTS procurement_inbox
(
    event_id CHAR(36) NOT NULL PRIMARY KEY,
    event_type VARCHAR(255) NOT NULL,
    processed_on_utc DATETIME(6) NOT NULL
);

CREATE TABLE IF NOT EXISTS procurement_outbox
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
    CONSTRAINT uq_procurement_outbox_event UNIQUE (event_id),
    INDEX ix_procurement_outbox_pending (processed_on_utc, available_on_utc, occurred_on_utc)
);

CREATE TABLE IF NOT EXISTS warehouse_expected_purchase_orders
(
    purchase_order_id CHAR(36) NOT NULL PRIMARY KEY,
    purchase_order_number VARCHAR(64) NOT NULL,
    supplier_id CHAR(36) NOT NULL,
    destination_organization_unit_id CHAR(36) NOT NULL,
    status VARCHAR(32) NOT NULL,
    issued_on_utc DATETIME(6) NOT NULL,
    updated_on_utc DATETIME(6) NOT NULL,
    CONSTRAINT uq_warehouse_expected_po_number UNIQUE (purchase_order_number),
    INDEX ix_warehouse_expected_destination_status (destination_organization_unit_id, status)
);

CREATE TABLE IF NOT EXISTS warehouse_expected_purchase_order_items
(
    purchase_order_id CHAR(36) NOT NULL,
    product_id CHAR(36) NOT NULL,
    quantity DECIMAL(18, 3) NOT NULL,
    unit_of_measure VARCHAR(16) NOT NULL,
    PRIMARY KEY (purchase_order_id, product_id),
    CONSTRAINT fk_warehouse_expected_po_item
        FOREIGN KEY (purchase_order_id) REFERENCES warehouse_expected_purchase_orders (purchase_order_id)
);

CREATE TABLE IF NOT EXISTS warehouse_goods_receipts
(
    id CHAR(36) NOT NULL PRIMARY KEY,
    goods_receipt_number VARCHAR(64) NOT NULL,
    purchase_order_id CHAR(36) NOT NULL,
    destination_organization_unit_id CHAR(36) NOT NULL,
    received_by_user_id CHAR(36) NOT NULL,
    received_on_utc DATETIME(6) NOT NULL,
    CONSTRAINT uq_warehouse_grn_number UNIQUE (goods_receipt_number),
    CONSTRAINT uq_warehouse_grn_po UNIQUE (purchase_order_id)
);

CREATE TABLE IF NOT EXISTS warehouse_goods_receipt_items
(
    goods_receipt_id CHAR(36) NOT NULL,
    product_id CHAR(36) NOT NULL,
    quantity DECIMAL(18, 3) NOT NULL,
    unit_of_measure VARCHAR(16) NOT NULL,
    PRIMARY KEY (goods_receipt_id, product_id),
    CONSTRAINT fk_warehouse_grn_item
        FOREIGN KEY (goods_receipt_id) REFERENCES warehouse_goods_receipts (id)
);

CREATE TABLE IF NOT EXISTS warehouse_inbox
(
    event_id CHAR(36) NOT NULL PRIMARY KEY,
    event_type VARCHAR(255) NOT NULL,
    processed_on_utc DATETIME(6) NOT NULL
);

CREATE TABLE IF NOT EXISTS warehouse_outbox
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
    CONSTRAINT uq_warehouse_outbox_event UNIQUE (event_id),
    INDEX ix_warehouse_outbox_pending (processed_on_utc, available_on_utc, occurred_on_utc)
);

CREATE TABLE IF NOT EXISTS inventory_location_stock
(
    organization_unit_id CHAR(36) NOT NULL,
    product_id CHAR(36) NOT NULL,
    on_hand_quantity DECIMAL(18, 3) NOT NULL,
    updated_on_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (organization_unit_id, product_id),
    CONSTRAINT chk_inventory_location_on_hand CHECK (on_hand_quantity >= 0)
);
