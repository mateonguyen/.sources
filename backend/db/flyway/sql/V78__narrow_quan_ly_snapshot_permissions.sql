-- V78: QUAN_LY (H05) only manages reporting side — remove submit/create/update on snapshot.
-- H05 receives reports, does not submit/enter data. Keeps snapshot:read + snapshot:pdf.
-- Business module create/update/delete/read for QUAN_LY already removed by V77.

DELETE FROM IDM_ROLE_PERMISSIONS rp
WHERE EXISTS (
    SELECT 1
    FROM IDM_ROLES r
    JOIN IDM_PERMISSIONS p ON p.ID = rp.PERMISSION_ID
    WHERE r.ID = rp.ROLE_ID
      AND r.NORMALIZED_NAME = 'QUAN_LY'
      AND p.PERM_CODE IN ('snapshot:create', 'snapshot:update', 'snapshot:submit')
);
