-- V95: Tai khoan cap tinh khong mac dinh duoc quan ly nguoi dung.
-- Quyen hien thi menu va truy cap API se do quan tri vien cap ro rang trong
-- man hinh Phan quyen, thay vi bi baseline seeder tu dong them lai.

DELETE FROM IDM_ROLE_PERMISSIONS rp
WHERE rp.ROLE_ID IN (
    SELECT r.ID
    FROM IDM_ROLES r
    WHERE UPPER(NVL(r.NORMALIZED_NAME, r.NAME)) = 'CAP_TINH'
)
AND rp.PERMISSION_ID IN (
    SELECT p.ID
    FROM IDM_PERMISSIONS p
    WHERE p.PERM_CODE IN ('users:read', 'users:create', 'users:update')
);
