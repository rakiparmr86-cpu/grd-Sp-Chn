-- Physical receipt is quarantined in Warehouse until Quality releases it.
-- Inventory is updated only from the quality-approved integration event.

CREATE TABLE IF NOT EXISTS warehouse_quality_inspections
(
    id CHAR(36) NOT NULL PRIMARY KEY,
    goods_receipt_id CHAR(36) NOT NULL,
    purchase_order_id CHAR(36) NOT NULL,
    destination_organization_unit_id CHAR(36) NOT NULL,
    inspected_by_user_id CHAR(36) NOT NULL,
    result VARCHAR(16) NOT NULL,
    notes VARCHAR(1000) NULL,
    inspected_on_utc DATETIME(6) NOT NULL,
    CONSTRAINT uq_warehouse_quality_grn UNIQUE (goods_receipt_id),
    CONSTRAINT fk_warehouse_quality_grn
        FOREIGN KEY (goods_receipt_id) REFERENCES warehouse_goods_receipts (id),
    CONSTRAINT chk_warehouse_quality_result
        CHECK (result IN ('Passed', 'Rejected')),
    INDEX ix_warehouse_quality_po (purchase_order_id),
    INDEX ix_warehouse_quality_location_result
        (destination_organization_unit_id, result, inspected_on_utc)
);

CREATE TABLE IF NOT EXISTS inventory_stock_movements
(
    id CHAR(36) NOT NULL PRIMARY KEY,
    event_id CHAR(36) NOT NULL,
    source_type VARCHAR(64) NOT NULL,
    source_id CHAR(36) NOT NULL,
    organization_unit_id CHAR(36) NOT NULL,
    product_id CHAR(36) NOT NULL,
    movement_type VARCHAR(32) NOT NULL,
    quantity DECIMAL(18, 3) NOT NULL,
    occurred_on_utc DATETIME(6) NOT NULL,
    CONSTRAINT uq_inventory_movement_event_product UNIQUE (event_id, product_id),
    CONSTRAINT chk_inventory_movement_quantity CHECK (quantity > 0),
    INDEX ix_inventory_movement_location_product
        (organization_unit_id, product_id, occurred_on_utc),
    INDEX ix_inventory_movement_source (source_type, source_id)
);

INSERT INTO identity_access_profiles
    (code, display_name, role_name, is_hr_assignable, is_active,
     created_on_utc, updated_on_utc)
VALUES
    ('QualityInspector', 'Quality Inspector', 'Executive', TRUE, TRUE,
     UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE
    display_name = VALUES(display_name),
    role_name = VALUES(role_name),
    is_hr_assignable = VALUES(is_hr_assignable),
    is_active = VALUES(is_active),
    updated_on_utc = UTC_TIMESTAMP(6);

INSERT INTO identity_permissions
    (code, display_name, module_name, description, is_active,
     created_on_utc, updated_on_utc)
VALUES
    ('warehouse.quality-inspection.read', 'View quality inspections', 'Warehouse',
     'View received material awaiting or completing quality inspection.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('warehouse.quality-inspection.post', 'Complete quality inspections', 'Warehouse',
     'Pass or reject physically received material before inventory release.',
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
    ('Director', 'warehouse.quality-inspection.read'),
    ('Director', 'warehouse.quality-inspection.post'),
    ('PlantSupervisor', 'warehouse.quality-inspection.read'),
    ('PlantSupervisor', 'warehouse.quality-inspection.post'),
    ('StoreExecutive', 'catalog.item.read'),
    ('StoreExecutive', 'warehouse.quality-inspection.read'),
    ('QualityInspector', 'organization.read'),
    ('QualityInspector', 'catalog.item.read'),
    ('QualityInspector', 'procurement.material-request.read'),
    ('QualityInspector', 'warehouse.goods-receipt.read'),
    ('QualityInspector', 'warehouse.quality-inspection.read'),
    ('QualityInspector', 'warehouse.quality-inspection.post');
