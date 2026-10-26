-- V96: Moi menu quan tri danh muc co mot bo quyen rieng.
-- Quyen catalog_customization:* cu dieu khien dong thoi ca Danh muc chung va
-- Loai thiet bi CNTT nen khong the hien/an va phan quyen doc lap theo menu.

-- Bao toan quyen xem: role tung duoc xem tuy chinh danh muc se duoc xem ca hai
-- man hinh. Quan tri vien co the tach lai tung man hinh trong ma tran phan quyen.
INSERT INTO IDM_ROLE_PERMISSIONS (ROLE_ID, PERMISSION_ID)
SELECT old_rp.ROLE_ID, new_p.ID
FROM IDM_ROLE_PERMISSIONS old_rp
JOIN IDM_PERMISSIONS old_p ON old_p.ID = old_rp.PERMISSION_ID
CROSS JOIN IDM_PERMISSIONS new_p
WHERE old_p.PERM_CODE = 'catalog_customization:read'
  AND new_p.PERM_CODE IN ('codes:read', 'ref_loai_thiet_bi:read')
  AND NOT EXISTS (
      SELECT 1
      FROM IDM_ROLE_PERMISSIONS existing_rp
      WHERE existing_rp.ROLE_ID = old_rp.ROLE_ID
        AND existing_rp.PERMISSION_ID = new_p.ID
  );

-- Quyen cap nhat cu cho phep them, sua, an va xoa muc tai don vi. Chuyen day
-- du sang cac hanh dong ro rang cua tung man hinh de khong mat nghiep vu cu.
INSERT INTO IDM_ROLE_PERMISSIONS (ROLE_ID, PERMISSION_ID)
SELECT old_rp.ROLE_ID, new_p.ID
FROM IDM_ROLE_PERMISSIONS old_rp
JOIN IDM_PERMISSIONS old_p ON old_p.ID = old_rp.PERMISSION_ID
CROSS JOIN IDM_PERMISSIONS new_p
WHERE old_p.PERM_CODE = 'catalog_customization:update'
  AND new_p.PERM_CODE IN (
      'codes:create', 'codes:update', 'codes:delete',
      'ref_loai_thiet_bi:create', 'ref_loai_thiet_bi:update', 'ref_loai_thiet_bi:delete'
  )
  AND NOT EXISTS (
      SELECT 1
      FROM IDM_ROLE_PERMISSIONS existing_rp
      WHERE existing_rp.ROLE_ID = old_rp.ROLE_ID
        AND existing_rp.PERMISSION_ID = new_p.ID
  );

UPDATE IDM_PERMISSIONS
SET MODULE = 'codes', MO_TA = N'Danh mục chung : ' ||
    CASE TO_NCHAR(ACTION)
        WHEN N'read' THEN N'Xem'
        WHEN N'create' THEN N'Thêm'
        WHEN N'update' THEN N'Sửa'
        WHEN N'delete' THEN N'Xóa'
        ELSE TO_NCHAR(ACTION)
    END
WHERE PERM_CODE IN ('codes:read', 'codes:create', 'codes:update', 'codes:delete');

UPDATE IDM_PERMISSIONS
SET MODULE = 'ref_loai_thiet_bi', MO_TA = N'Loại thiết bị CNTT : ' ||
    CASE TO_NCHAR(ACTION)
        WHEN N'read' THEN N'Xem'
        WHEN N'create' THEN N'Thêm'
        WHEN N'update' THEN N'Sửa'
        WHEN N'delete' THEN N'Xóa'
        ELSE TO_NCHAR(ACTION)
    END
WHERE PERM_CODE IN (
    'ref_loai_thiet_bi:read',
    'ref_loai_thiet_bi:create',
    'ref_loai_thiet_bi:update',
    'ref_loai_thiet_bi:delete'
);

DELETE FROM IDM_ROLE_PERMISSIONS
WHERE PERMISSION_ID IN (
    SELECT ID
    FROM IDM_PERMISSIONS
    WHERE PERM_CODE IN ('catalog_customization:read', 'catalog_customization:update')
);

DELETE FROM IDM_PERMISSIONS
WHERE PERM_CODE IN ('catalog_customization:read', 'catalog_customization:update');
