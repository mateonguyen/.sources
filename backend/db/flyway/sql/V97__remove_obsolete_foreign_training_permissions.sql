-- V97: Xoa nhom quyen cu khong con man hinh/API tuong ung.
-- Dao tao hoc vien nuoc ngoai da duoc hop nhat vao module Dao tao hoc vien.

DELETE FROM IDM_ROLE_PERMISSIONS
WHERE PERMISSION_ID IN (
    SELECT ID
    FROM IDM_PERMISSIONS
    WHERE MODULE = 'dao_tao_nuoc_ngoai'
       OR PERM_CODE LIKE 'dao_tao_nuoc_ngoai:%'
);

DELETE FROM IDM_PERMISSIONS
WHERE MODULE = 'dao_tao_nuoc_ngoai'
   OR PERM_CODE LIKE 'dao_tao_nuoc_ngoai:%';
