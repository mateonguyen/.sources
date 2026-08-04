-- V77: Restrict business module access for QUAN_LY and SYSTEM_ADMIN.
-- All business data should be consumed through Tra cuu bao cao.

DELETE FROM IDM_ROLE_PERMISSIONS rp
WHERE EXISTS (
    SELECT 1
    FROM IDM_ROLES r
    JOIN IDM_PERMISSIONS p ON p.ID = rp.PERMISSION_ID
    WHERE r.ID = rp.ROLE_ID
      AND r.NORMALIZED_NAME IN ('QUAN_LY', 'SYSTEM_ADMIN')
      AND (
        p.PERM_CODE LIKE 'nhan_luc_cntt:%'
        OR p.PERM_CODE LIKE 'dao_tao_boi_duong:%'
        OR p.PERM_CODE LIKE 'dao_tao_hoc_vien:%'
        OR p.PERM_CODE LIKE 'nang_luc_so:%'
        OR p.PERM_CODE LIKE 'thiet_bi_cntt:%'
        OR p.PERM_CODE LIKE 'he_thong_thong_tin:%'
        OR p.PERM_CODE LIKE 'ha_tang_mang:%'
        OR p.PERM_CODE LIKE 'giam_sat_noc:%'
        OR p.PERM_CODE LIKE 'camera_quan_ly:%'
        OR p.PERM_CODE LIKE 'camera_thuc_trang:%'
        OR p.PERM_CODE LIKE 'du_an_cntt:%'
        OR p.PERM_CODE LIKE 'van_ban_qppl:%'
        OR p.PERM_CODE LIKE 'giam_sat_soc:%'
        OR p.PERM_CODE LIKE 'attt_httt_van_hanh:%'
        OR p.PERM_CODE LIKE 'attt_httt_dau_tu:%'
        OR p.PERM_CODE LIKE 'giai_phap_attt:%'
      )
);
