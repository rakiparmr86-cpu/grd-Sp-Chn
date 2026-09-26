-- Stock Ledger (GRD-025-sl) is read by every manager-level profile and by the
-- departments that handle stock. Directors, the Regional General Manager, the
-- Purchase Manager, Plant/Store Supervisor, Store Executive and Consumption Unit
-- Executive already hold inventory.stock.read; this adds the remaining ones.
-- Idempotent; remove a row from identity_access_profile_permissions (or use the
-- Manage permissions screen) to withdraw access again.

INSERT IGNORE INTO identity_access_profile_permissions
    (access_profile_code, permission_code)
SELECT profile.code, 'inventory.stock.read'
FROM identity_access_profiles profile
WHERE profile.code IN ('FinanceManager', 'HrManager', 'QualityInspector')
  AND EXISTS (
      SELECT 1 FROM identity_permissions permission
      WHERE permission.code = 'inventory.stock.read');
