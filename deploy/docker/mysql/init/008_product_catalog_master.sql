-- Product Catalog owns reusable item, category, and UOM master data.
-- Procurement persists stable item IDs plus transactional quantity/UOM snapshots;
-- it deliberately has no database FK to another service's tables.

CREATE TABLE IF NOT EXISTS catalog_units_of_measure
(
    code VARCHAR(16) NOT NULL PRIMARY KEY,
    name VARCHAR(80) NOT NULL,
    measurement_type VARCHAR(32) NOT NULL,
    decimal_places TINYINT UNSIGNED NOT NULL DEFAULT 3,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_on_utc DATETIME(6) NOT NULL,
    updated_on_utc DATETIME(6) NOT NULL
);

CREATE TABLE IF NOT EXISTS catalog_categories
(
    code VARCHAR(32) NOT NULL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description VARCHAR(300) NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_on_utc DATETIME(6) NOT NULL,
    updated_on_utc DATETIME(6) NOT NULL
);

CREATE TABLE IF NOT EXISTS catalog_items
(
    id CHAR(36) NOT NULL PRIMARY KEY,
    code VARCHAR(48) NOT NULL,
    name VARCHAR(160) NOT NULL,
    description VARCHAR(500) NULL,
    category_code VARCHAR(32) NOT NULL,
    base_uom_code VARCHAR(16) NOT NULL,
    procurement_allowed BOOLEAN NOT NULL DEFAULT TRUE,
    inventory_tracked BOOLEAN NOT NULL DEFAULT TRUE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_on_utc DATETIME(6) NOT NULL,
    updated_on_utc DATETIME(6) NOT NULL,
    CONSTRAINT uq_catalog_item_code UNIQUE (code),
    CONSTRAINT fk_catalog_item_category
        FOREIGN KEY (category_code) REFERENCES catalog_categories (code),
    CONSTRAINT fk_catalog_item_uom
        FOREIGN KEY (base_uom_code) REFERENCES catalog_units_of_measure (code),
    INDEX ix_catalog_item_active_name (is_active, name),
    INDEX ix_catalog_item_category (category_code, is_active)
);

INSERT INTO catalog_units_of_measure
    (code, name, measurement_type, decimal_places, is_active,
     created_on_utc, updated_on_utc)
VALUES
    ('BAG', 'Bag', 'Count', 0, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('EA', 'Each', 'Count', 0, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('KG', 'Kilogram', 'Weight', 3, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('MT', 'Metric Tonne', 'Weight', 3, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('LTR', 'Litre', 'Volume', 3, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE
    name = VALUES(name),
    measurement_type = VALUES(measurement_type),
    decimal_places = VALUES(decimal_places),
    is_active = VALUES(is_active),
    updated_on_utc = UTC_TIMESTAMP(6);

INSERT INTO catalog_categories
    (code, name, description, is_active, created_on_utc, updated_on_utc)
VALUES
    ('PACKAGING', 'Packaging', 'Bags and other production packaging materials.', TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('RAW_MATERIAL', 'Raw Material', 'Materials purchased for production or processing.', TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('FUEL', 'Fuel', 'Fuel consumed by plants and boiler units.', TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('STORE_CONSUMABLE', 'Store Consumable', 'General materials controlled and issued by a branch store.', TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE
    name = VALUES(name),
    description = VALUES(description),
    is_active = VALUES(is_active),
    updated_on_utc = UTC_TIMESTAMP(6);

INSERT INTO catalog_items
    (id, code, name, description, category_code, base_uom_code,
     procurement_allowed, inventory_tracked, is_active,
     created_on_utc, updated_on_utc)
VALUES
    ('30000000-0000-0000-0000-000000000001', 'PKG-BAG-70KG', 'Packing Bag - 70 kg',
     'Production packing bag with 70 kg capacity.', 'PACKAGING', 'BAG',
     TRUE, TRUE, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('30000000-0000-0000-0000-000000000002', 'RAW-COAL', 'Production Coal',
     'Coal purchased for production consumption.', 'FUEL', 'MT',
     TRUE, TRUE, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('30000000-0000-0000-0000-000000000003', 'FUEL-FURNACE-OIL', 'Furnace Oil',
     'Liquid furnace fuel controlled by the store.', 'FUEL', 'LTR',
     TRUE, TRUE, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('30000000-0000-0000-0000-000000000004', 'STORE-MAIZE', 'Maize',
     'Maize grain purchased and received through the branch store.', 'RAW_MATERIAL', 'KG',
     TRUE, TRUE, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('30000000-0000-0000-0000-000000000005', 'STORE-RICE', 'Rice',
     'Rice purchased and received through the branch store.', 'STORE_CONSUMABLE', 'KG',
     TRUE, TRUE, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('30000000-0000-0000-0000-000000000006', 'STORE-EDIBLE-OIL', 'Edible Oil',
     'Edible oil purchased and received through the branch store.', 'STORE_CONSUMABLE', 'LTR',
     TRUE, TRUE, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE
    code = VALUES(code),
    name = VALUES(name),
    description = VALUES(description),
    category_code = VALUES(category_code),
    base_uom_code = VALUES(base_uom_code),
    procurement_allowed = VALUES(procurement_allowed),
    inventory_tracked = VALUES(inventory_tracked),
    is_active = VALUES(is_active),
    updated_on_utc = UTC_TIMESTAMP(6);

INSERT INTO identity_permissions
    (code, display_name, module_name, description, is_active,
     created_on_utc, updated_on_utc)
VALUES
    ('catalog.item.read', 'View item master', 'ProductCatalog',
     'View active procurement items, categories, and units of measure.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('catalog.item.manage', 'Manage item master', 'ProductCatalog',
     'Create and maintain items, categories, and units of measure.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE
    display_name = VALUES(display_name),
    module_name = VALUES(module_name),
    description = VALUES(description),
    is_active = VALUES(is_active),
    updated_on_utc = UTC_TIMESTAMP(6);

INSERT IGNORE INTO identity_access_profile_permissions
    (access_profile_code, permission_code)
VALUES
    ('Director', 'catalog.item.read'),
    ('Director', 'catalog.item.manage'),
    ('RegionalGeneralManager', 'catalog.item.read'),
    ('PurchaseManager', 'catalog.item.read'),
    ('PlantSupervisor', 'catalog.item.read'),
    ('BoilerExecutive', 'catalog.item.read');
