-- V79: "Tiến độ tổng hợp" (tong_hop_tien_do) chỉ dành cho CAP_TINH theo dõi tiến độ
-- tổng hợp từ các đơn vị CAP_XA. QUAN_LY (H05) không dùng chức năng này.

DELETE FROM IDM_ROLE_PERMISSIONS rp
WHERE EXISTS (
    SELECT 1
    FROM IDM_ROLES r
    JOIN IDM_PERMISSIONS p ON p.ID = rp.PERMISSION_ID
    WHERE r.ID = rp.ROLE_ID
      AND r.NORMALIZED_NAME = 'QUAN_LY'
      AND p.PERM_CODE = 'tong_hop_tien_do:read'
);
