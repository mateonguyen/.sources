-- V72: Align role-permission data with approved matrix.
-- - QUAN_LY must have ky_bao_cao:update
-- - CAP_XA must not have du_an_cntt:delete

INSERT INTO IDM_ROLE_PERMISSIONS (ROLE_ID, PERMISSION_ID)
SELECT r.ID, p.ID
FROM IDM_ROLES r
JOIN IDM_PERMISSIONS p
  ON p.PERM_CODE = 'ky_bao_cao:update'
WHERE r.NORMALIZED_NAME = 'QUAN_LY'
  AND NOT EXISTS (
    SELECT 1
    FROM IDM_ROLE_PERMISSIONS rp
    WHERE rp.ROLE_ID = r.ID
      AND rp.PERMISSION_ID = p.ID
  );

DELETE FROM IDM_ROLE_PERMISSIONS rp
WHERE EXISTS (
    SELECT 1
    FROM IDM_ROLES r
    JOIN IDM_PERMISSIONS p
      ON p.PERM_CODE = 'du_an_cntt:delete'
    WHERE r.NORMALIZED_NAME = 'CAP_XA'
      AND rp.ROLE_ID = r.ID
      AND rp.PERMISSION_ID = p.ID
);
