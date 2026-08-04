-- V69: Grant DU_AN_CNTT permissions to business roles so the module appears by default.
-- This keeps Flyway-managed databases aligned with the runtime baseline seeder.

INSERT INTO IDM_ROLE_PERMISSIONS (ROLE_ID, PERMISSION_ID)
SELECT r.ID, p.ID
FROM IDM_ROLES r
JOIN IDM_PERMISSIONS p
  ON p.PERM_CODE IN (
    'du_an_cntt:read',
    'du_an_cntt:create',
    'du_an_cntt:update',
    'du_an_cntt:delete'
  )
WHERE r.NORMALIZED_NAME IN ('QUAN_LY', 'CAP_TINH', 'CAP_XA')
  AND NOT EXISTS (
    SELECT 1
    FROM IDM_ROLE_PERMISSIONS rp
    WHERE rp.ROLE_ID = r.ID
      AND rp.PERMISSION_ID = p.ID
  );