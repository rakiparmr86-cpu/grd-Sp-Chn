-- Adds the HR user-management permission and aligns local demo passwords.
-- Safe to execute manually against an existing grd_local database.

UPDATE identity_users
SET password_hash = 'pbkdf2-sha256$100000$R1JELWxvY2FsLXNlZWQtc2FsdC0yMDI2$mEAf2fEzJ6I2K6etT9SQ8VbHO0Ty7qnoiSiR1KpuE8c='
WHERE normalized_user_name IN
(
    'DIRECTOR@GRD.LOCAL',
    'GM.NORTH@GRD.LOCAL',
    'MANAGER.PURCHASE@GRD.LOCAL',
    'SUPERVISOR.PLANT@GRD.LOCAL',
    'EXECUTIVE.BOILER@GRD.LOCAL',
    'MANAGER.HR@GRD.LOCAL'
);

INSERT IGNORE INTO identity_users
    (id, user_name, normalized_user_name, password_hash, role_name,
     organization_unit_id, is_active, created_on_utc)
VALUES
    ('10000000-0000-0000-0000-000000000006', 'manager.hr@grd.local', 'MANAGER.HR@GRD.LOCAL', 'pbkdf2-sha256$100000$R1JELWxvY2FsLXNlZWQtc2FsdC0yMDI2$mEAf2fEzJ6I2K6etT9SQ8VbHO0Ty7qnoiSiR1KpuE8c=', 'Manager', '00000000-0000-0000-0000-000000000002', TRUE, UTC_TIMESTAMP(6));

INSERT IGNORE INTO identity_user_permissions (user_id, permission_code)
VALUES
    ('10000000-0000-0000-0000-000000000001', 'identity.user.create'),
    ('10000000-0000-0000-0000-000000000006', 'organization.read'),
    ('10000000-0000-0000-0000-000000000006', 'identity.user.create');
