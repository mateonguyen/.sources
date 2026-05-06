-- V46: Chuan hoa quyen cho CAP_XA
-- PHONG/XA chi nhap lieu va bat co xac nhan, khong duoc tao/sua/nop bao cao snapshot.
-- Luu y: Bang quyen da duoc doi ten sang IDM_* tu V4.

DELETE FROM IDM_ROLE_PERMISSIONS
WHERE ROLE_ID = (
        SELECT ID
        FROM IDM_ROLES
    WHERE NORMALIZED_NAME = 'CAP_XA'
    )
  AND PERMISSION_ID IN (
      SELECT ID
      FROM IDM_PERMISSIONS
      WHERE PERM_CODE IN ('snapshot:create', 'snapshot:update', 'snapshot:submit')
  );
