-- Database-owned Identity access profiles.
-- Login resolves role and permissions from these tables; the browser never chooses them.

CREATE TABLE IF NOT EXISTS identity_access_profiles
(
    code VARCHAR(64) NOT NULL PRIMARY KEY,
    display_name VARCHAR(160) NOT NULL,
    role_name VARCHAR(64) NOT NULL,
    is_hr_assignable BOOLEAN NOT NULL DEFAULT FALSE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_on_utc DATETIME(6) NOT NULL,
    updated_on_utc DATETIME(6) NOT NULL
);

CREATE TABLE IF NOT EXISTS identity_access_profile_permissions
(
    access_profile_code VARCHAR(64) NOT NULL,
    permission_code VARCHAR(128) NOT NULL,
    PRIMARY KEY (access_profile_code, permission_code),
    CONSTRAINT fk_identity_profile_permission_profile
        FOREIGN KEY (access_profile_code) REFERENCES identity_access_profiles (code)
);

INSERT INTO identity_access_profiles
    (code, display_name, role_name, is_hr_assignable, is_active,
     created_on_utc, updated_on_utc)
VALUES
    ('Director', 'Director', 'Director', FALSE, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('RegionalGeneralManager', 'Regional General Manager', 'GeneralManager', FALSE, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('HrManager', 'HR Manager', 'Manager', TRUE, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('PurchaseManager', 'Purchase Manager', 'Manager', TRUE, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('PlantSupervisor', 'Plant / Store Supervisor', 'Supervisor', TRUE, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('StoreExecutive', 'Store Executive', 'Executive', TRUE, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('BoilerExecutive', 'Consumption Unit Executive', 'Executive', TRUE, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE
    display_name = VALUES(display_name),
    role_name = VALUES(role_name),
    is_hr_assignable = VALUES(is_hr_assignable),
    is_active = VALUES(is_active),
    updated_on_utc = UTC_TIMESTAMP(6);

INSERT IGNORE INTO identity_access_profile_permissions
    (access_profile_code, permission_code)
VALUES
    ('Director', 'identity.user.create'),
    ('Director', 'organization.read'),
    ('Director', 'organization.manage'),
    ('Director', 'procurement.material-request.create'),
    ('Director', 'procurement.material-request.read'),
    ('Director', 'procurement.material-request.approve'),
    ('Director', 'procurement.purchase-order.create'),
    ('Director', 'procurement.purchase-order.read'),
    ('Director', 'inventory.stock.read'),
    ('Director', 'warehouse.goods-receipt.read'),
    ('Director', 'warehouse.goods-receipt.post'),

    ('RegionalGeneralManager', 'organization.read'),
    ('RegionalGeneralManager', 'procurement.material-request.read'),
    ('RegionalGeneralManager', 'procurement.material-request.approve'),
    ('RegionalGeneralManager', 'procurement.purchase-order.create'),
    ('RegionalGeneralManager', 'procurement.purchase-order.read'),
    ('RegionalGeneralManager', 'inventory.stock.read'),

    ('HrManager', 'organization.read'),
    ('HrManager', 'identity.user.create'),

    ('PurchaseManager', 'organization.read'),
    ('PurchaseManager', 'procurement.material-request.read'),
    ('PurchaseManager', 'procurement.material-request.approve'),
    ('PurchaseManager', 'procurement.purchase-order.create'),
    ('PurchaseManager', 'procurement.purchase-order.read'),
    ('PurchaseManager', 'inventory.stock.read'),

    ('PlantSupervisor', 'organization.read'),
    ('PlantSupervisor', 'procurement.material-request.create'),
    ('PlantSupervisor', 'procurement.material-request.read'),
    ('PlantSupervisor', 'inventory.stock.read'),
    ('PlantSupervisor', 'warehouse.goods-receipt.read'),
    ('PlantSupervisor', 'warehouse.goods-receipt.post'),

    ('StoreExecutive', 'organization.read'),
    ('StoreExecutive', 'procurement.material-request.create'),
    ('StoreExecutive', 'procurement.material-request.read'),
    ('StoreExecutive', 'inventory.stock.read'),
    ('StoreExecutive', 'warehouse.goods-receipt.read'),
    ('StoreExecutive', 'warehouse.goods-receipt.post'),

    ('BoilerExecutive', 'organization.read'),
    ('BoilerExecutive', 'procurement.material-request.create'),
    ('BoilerExecutive', 'procurement.material-request.read'),
    ('BoilerExecutive', 'inventory.stock.read');

SET @identity_profile_column_exists =
(
    SELECT COUNT(*)
    FROM information_schema.columns
    WHERE table_schema = DATABASE()
      AND table_name = 'identity_users'
      AND column_name = 'access_profile_code'
);
SET @identity_profile_column_sql = IF(
    @identity_profile_column_exists = 0,
    'ALTER TABLE identity_users ADD COLUMN access_profile_code VARCHAR(64) NULL AFTER role_name',
    'SELECT 1');
PREPARE identity_profile_column_statement FROM @identity_profile_column_sql;
EXECUTE identity_profile_column_statement;
DEALLOCATE PREPARE identity_profile_column_statement;

UPDATE identity_users
SET access_profile_code = CASE normalized_user_name
    WHEN 'DIRECTOR@GRD.LOCAL' THEN 'Director'
    WHEN 'GM.NORTH@GRD.LOCAL' THEN 'RegionalGeneralManager'
    WHEN 'MANAGER.PURCHASE@GRD.LOCAL' THEN 'PurchaseManager'
    WHEN 'SUPERVISOR.PLANT@GRD.LOCAL' THEN 'PlantSupervisor'
    WHEN 'EXECUTIVE.BOILER@GRD.LOCAL' THEN 'BoilerExecutive'
    WHEN 'MANAGER.HR@GRD.LOCAL' THEN 'HrManager'
    ELSE access_profile_code
END;

SET @identity_profile_index_exists =
(
    SELECT COUNT(*)
    FROM information_schema.statistics
    WHERE table_schema = DATABASE()
      AND table_name = 'identity_users'
      AND index_name = 'ix_identity_user_access_profile'
);
SET @identity_profile_index_sql = IF(
    @identity_profile_index_exists = 0,
    'ALTER TABLE identity_users ADD INDEX ix_identity_user_access_profile (access_profile_code)',
    'SELECT 1');
PREPARE identity_profile_index_statement FROM @identity_profile_index_sql;
EXECUTE identity_profile_index_statement;
DEALLOCATE PREPARE identity_profile_index_statement;

SET @identity_profile_fk_exists =
(
    SELECT COUNT(*)
    FROM information_schema.table_constraints
    WHERE constraint_schema = DATABASE()
      AND table_name = 'identity_users'
      AND constraint_name = 'fk_identity_user_access_profile'
      AND constraint_type = 'FOREIGN KEY'
);
SET @identity_profile_fk_sql = IF(
    @identity_profile_fk_exists = 0,
    'ALTER TABLE identity_users ADD CONSTRAINT fk_identity_user_access_profile FOREIGN KEY (access_profile_code) REFERENCES identity_access_profiles (code)',
    'SELECT 1');
PREPARE identity_profile_fk_statement FROM @identity_profile_fk_sql;
EXECUTE identity_profile_fk_statement;
DEALLOCATE PREPARE identity_profile_fk_statement;

-- identity_user_permissions is retained temporarily for migration compatibility.
-- Runtime authorization now reads identity_access_profile_permissions instead.
