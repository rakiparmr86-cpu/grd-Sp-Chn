-- Backend-owned permission catalog and safe dynamic profile-permission management.

CREATE TABLE IF NOT EXISTS identity_permissions
(
    code VARCHAR(128) NOT NULL PRIMARY KEY,
    display_name VARCHAR(160) NOT NULL,
    module_name VARCHAR(80) NOT NULL,
    description VARCHAR(500) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_on_utc DATETIME(6) NOT NULL,
    updated_on_utc DATETIME(6) NOT NULL
);

INSERT INTO identity_permissions
    (code, display_name, module_name, description, is_active,
     created_on_utc, updated_on_utc)
VALUES
    ('identity.user.create', 'Create employee users', 'Identity',
     'Create users and assign an HR-approved access profile and organization unit.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('identity.access-profile.manage', 'Manage access-profile permissions', 'Identity',
     'Add or remove permissions assigned to Identity access profiles.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('organization.read', 'View organization structure', 'Organization',
     'View enterprise, region, branch, plant, warehouse, and consumption-unit hierarchy.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('organization.manage', 'Manage organization structure', 'Organization',
     'Create and maintain organization units and hierarchy.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('procurement.material-request.create', 'Create material requests', 'Procurement',
     'Raise a material request for a store or consumption unit.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('procurement.material-request.read', 'View material requests', 'Procurement',
     'View material requests in the authorized organization scope.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('procurement.material-request.approve', 'Approve material requests', 'Procurement',
     'Approve or reject material requests before purchasing.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('procurement.purchase-order.create', 'Create purchase orders', 'Procurement',
     'Create a vendor purchase order from an approved requirement.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('procurement.purchase-order.read', 'View purchase orders', 'Procurement',
     'View purchase orders in the authorized organization scope.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('inventory.stock.read', 'View inventory stock', 'Inventory',
     'View on-hand stock for authorized locations.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('warehouse.goods-receipt.read', 'View goods receipts', 'Warehouse',
     'View material receipts posted against purchase orders.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('warehouse.goods-receipt.post', 'Post goods receipts', 'Warehouse',
     'Receive vendor material and post the goods receipt transaction.',
     TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE
    display_name = VALUES(display_name),
    module_name = VALUES(module_name),
    description = VALUES(description),
    updated_on_utc = UTC_TIMESTAMP(6);

-- Keep one non-removable administrative path. Application validation also enforces
-- that the Director profile cannot lose this permission during a dynamic update.
INSERT IGNORE INTO identity_access_profile_permissions
    (access_profile_code, permission_code)
VALUES
    ('Director', 'identity.access-profile.manage');

SET @identity_permission_fk_exists =
(
    SELECT COUNT(*)
    FROM information_schema.table_constraints
    WHERE constraint_schema = DATABASE()
      AND table_name = 'identity_access_profile_permissions'
      AND constraint_name = 'fk_identity_profile_permission_catalog'
      AND constraint_type = 'FOREIGN KEY'
);
SET @identity_permission_fk_sql = IF(
    @identity_permission_fk_exists = 0,
    'ALTER TABLE identity_access_profile_permissions ADD CONSTRAINT fk_identity_profile_permission_catalog FOREIGN KEY (permission_code) REFERENCES identity_permissions (code)',
    'SELECT 1');
PREPARE identity_permission_fk_statement FROM @identity_permission_fk_sql;
EXECUTE identity_permission_fk_statement;
DEALLOCATE PREPARE identity_permission_fk_statement;
