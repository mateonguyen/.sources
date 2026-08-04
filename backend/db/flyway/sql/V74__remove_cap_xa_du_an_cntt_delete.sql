-- V74: Enforce CAP_XA matrix rule: must not have du_an_cntt:delete.

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
