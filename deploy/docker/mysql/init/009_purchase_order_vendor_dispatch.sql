-- Vendor dispatch is physically performed by the supplier, but recorded by an
-- authenticated internal Purchase Department user. No supplier ERP login is needed.

CREATE TABLE IF NOT EXISTS procurement_purchase_order_dispatches
(
    id CHAR(36) NOT NULL PRIMARY KEY,
    purchase_order_id CHAR(36) NOT NULL,
    supplier_id CHAR(36) NOT NULL,
    recorded_by_user_id CHAR(36) NOT NULL,
    vendor_dispatch_reference VARCHAR(80) NOT NULL,
    delivery_challan_number VARCHAR(80) NULL,
    transporter_name VARCHAR(160) NULL,
    vehicle_number VARCHAR(40) NULL,
    dispatched_on_utc DATETIME(6) NOT NULL,
    expected_delivery_on_utc DATETIME(6) NULL,
    notes VARCHAR(500) NULL,
    recorded_on_utc DATETIME(6) NOT NULL,
    CONSTRAINT uq_procurement_po_dispatch_po UNIQUE (purchase_order_id),
    CONSTRAINT fk_procurement_po_dispatch_po
        FOREIGN KEY (purchase_order_id) REFERENCES procurement_purchase_orders (id),
    INDEX ix_procurement_po_dispatch_supplier (supplier_id, dispatched_on_utc),
    INDEX ix_procurement_po_dispatch_reference (vendor_dispatch_reference)
);

INSERT INTO identity_permissions
    (code, display_name, module_name, description, is_active,
     created_on_utc, updated_on_utc)
VALUES
    ('procurement.purchase-order.dispatch', 'Record vendor dispatch', 'Procurement',
     'Record dispatch advice received from a supplier against an issued purchase order.',
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
    ('Director', 'procurement.purchase-order.dispatch'),
    ('RegionalGeneralManager', 'procurement.purchase-order.dispatch'),
    ('PurchaseManager', 'procurement.purchase-order.dispatch');
