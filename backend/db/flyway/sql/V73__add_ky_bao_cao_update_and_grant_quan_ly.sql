-- V73: Ensure ky_bao_cao:update exists and is granted to QUAN_LY.

INSERT INTO IDM_PERMISSIONS (ID, PERM_CODE, MODULE, ACTION, MO_TA)
SELECT NVL(MAX(ID), 3000) + 1,
       'ky_bao_cao:update',
       'ky_bao_cao',
       'update',
       'Baseline permission ky_bao_cao:update'
FROM IDM_PERMISSIONS
WHERE NOT EXISTS (
    SELECT 1
    FROM IDM_PERMISSIONS
    WHERE PERM_CODE = 'ky_bao_cao:update'
);

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
