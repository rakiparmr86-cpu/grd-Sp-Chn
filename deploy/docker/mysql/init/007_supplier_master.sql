-- Supplier bounded-context master data and test suppliers.
-- Safe to run repeatedly against an existing grd_local database.

CREATE TABLE IF NOT EXISTS supplier_master
(
    id CHAR(36) NOT NULL PRIMARY KEY,
    code VARCHAR(40) NOT NULL,
    legal_name VARCHAR(200) NOT NULL,
    display_name VARCHAR(160) NOT NULL,
    tax_identification_number VARCHAR(40) NULL,
    email VARCHAR(254) NULL,
    phone VARCHAR(40) NULL,
    address_line_1 VARCHAR(240) NULL,
    city VARCHAR(100) NULL,
    state_name VARCHAR(100) NULL,
    postal_code VARCHAR(20) NULL,
    country_code CHAR(2) NOT NULL DEFAULT 'IN',
    payment_terms_days INT NOT NULL DEFAULT 30,
    default_currency CHAR(3) NOT NULL DEFAULT 'INR',
    status VARCHAR(24) NOT NULL DEFAULT 'Active',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_on_utc DATETIME(6) NOT NULL,
    updated_on_utc DATETIME(6) NOT NULL,
    CONSTRAINT uq_supplier_master_code UNIQUE (code),
    INDEX ix_supplier_master_name (display_name),
    INDEX ix_supplier_master_status (is_active, status)
);

INSERT INTO supplier_master
    (id, code, legal_name, display_name, tax_identification_number,
     email, phone, address_line_1, city, state_name, postal_code,
     country_code, payment_terms_days, default_currency, status, is_active,
     created_on_utc, updated_on_utc)
VALUES
    ('20000000-0000-0000-0000-000000000001', 'SUP-ABACUS',
     'Abacus Supplies Private Limited', 'Abacus', 'TEST-GSTIN-ABACUS',
     'vendor.abacus@yopmail.com', '9990000001', 'Test Industrial Area',
     'New Delhi', 'Delhi', '110001', 'IN', 30, 'INR', 'Active', TRUE,
     UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('20000000-0000-0000-0000-000000000002', 'SUP-GRD',
     'GRD Materials Limited', 'GRD', 'TEST-GSTIN-GRD',
     'vendor.grd@yopmail.com', '9990000002', 'Test Manufacturing Estate',
     'Gurugram', 'Haryana', '122001', 'IN', 30, 'INR', 'Active', TRUE,
     UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('20000000-0000-0000-0000-000000000003', 'SUP-AU',
     'AU Industrial Supplies Limited', 'AU', 'TEST-GSTIN-AU',
     'vendor.au@yopmail.com', '9990000003', 'Test Commercial Zone',
     'Jaipur', 'Rajasthan', '302001', 'IN', 45, 'INR', 'Active', TRUE,
     UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('20000000-0000-0000-0000-000000000004', 'SUP-IDFC',
     'IDFC Trade Services Limited', 'IDFC', 'TEST-GSTIN-IDFC',
     'vendor.idfc@yopmail.com', '9990000004', 'Test Business District',
     'Mumbai', 'Maharashtra', '400001', 'IN', 45, 'INR', 'Active', TRUE,
     UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE
    legal_name = VALUES(legal_name),
    display_name = VALUES(display_name),
    tax_identification_number = VALUES(tax_identification_number),
    email = VALUES(email),
    phone = VALUES(phone),
    address_line_1 = VALUES(address_line_1),
    city = VALUES(city),
    state_name = VALUES(state_name),
    postal_code = VALUES(postal_code),
    country_code = VALUES(country_code),
    payment_terms_days = VALUES(payment_terms_days),
    default_currency = VALUES(default_currency),
    status = VALUES(status),
    is_active = VALUES(is_active),
    updated_on_utc = UTC_TIMESTAMP(6);

INSERT INTO identity_permissions
    (code, display_name, module_name, description, is_active,
     created_on_utc, updated_on_utc)
VALUES
    ('supplier.read', 'View supplier master', 'Supplier',
     'View active suppliers when sourcing and creating purchase orders.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('supplier.manage', 'Manage supplier master', 'Supplier',
     'Create, update, activate, block, and maintain supplier commercial details.',
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
    ('Director', 'supplier.read'),
    ('Director', 'supplier.manage'),
    ('RegionalGeneralManager', 'supplier.read'),
    ('PurchaseManager', 'supplier.read');
