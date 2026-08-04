-- V81: bao_cao:read / bao_cao:export không gắn với menu FE hay [HasPermission] nào
-- trong backend (đã kiểm tra toàn bộ codebase) -- quyền mồ côi, dọn dẹp hẳn.

DELETE FROM IDM_ROLE_PERMISSIONS rp
WHERE EXISTS (
    SELECT 1
    FROM IDM_PERMISSIONS p
    WHERE p.ID = rp.PERMISSION_ID
      AND p.PERM_CODE IN ('bao_cao:read', 'bao_cao:export')
);

DELETE FROM IDM_PERMISSIONS
WHERE PERM_CODE IN ('bao_cao:read', 'bao_cao:export');
