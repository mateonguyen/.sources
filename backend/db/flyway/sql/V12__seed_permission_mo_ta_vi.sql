-- Standardize IDM_PERMISSIONS.MO_TA in format: "<Feature Vietnamese> : <Action Vietnamese>"
-- This keeps translation in DB and avoids runtime translation workload on API.
UPDATE IDM_PERMISSIONS p
SET p.MO_TA =
    (
      CASE LOWER(TRIM(TO_CHAR(p.MODULE)))
        WHEN 'users' THEN 'Người dùng'
        WHEN 'roles' THEN 'Vai trò'
        WHEN 'permissions' THEN 'Phân quyền'
        WHEN 'phan_quyen' THEN 'Phân quyền'
        WHEN 'codes' THEN 'Danh mục'
        WHEN 'don_vi' THEN 'Đơn vị'
        WHEN 'ky_bao_cao' THEN 'Kỳ báo cáo'
        WHEN 'bao_cao' THEN 'Báo cáo'
        WHEN 'snapshot' THEN 'Snapshot'
        WHEN 'nhan_luc_cntt' THEN 'Nhân lực CNTT'
        WHEN 'nang_luc_so' THEN 'Năng lực số'
        WHEN 'dao_tao_boi_duong' THEN 'Đào tạo bồi dưỡng'
        WHEN 'dao_tao_hoc_vien' THEN 'Đào tạo học viên'
        WHEN 'dao_tao_nuoc_ngoai' THEN 'Đào tạo nước ngoài'
        WHEN 'he_thong_thong_tin' THEN 'Hệ thống thông tin'
        WHEN 'du_an_cntt' THEN 'Dự án CNTT'
        WHEN 'ha_tang_mang' THEN 'Hạ tầng mạng'
        WHEN 'thiet_bi_cntt' THEN 'Thiết bị CNTT'
        WHEN 'camera_thuc_trang' THEN 'Camera thực trạng'
        WHEN 'camera_quan_ly' THEN 'Camera quản lý'
        WHEN 'van_ban_qppl' THEN 'Văn bản quy phạm pháp luật'
        WHEN 'van_ban_den' THEN 'Văn bản đến'
        WHEN 'van_ban_di' THEN 'Văn bản đi'
        WHEN 'giam_sat_soc' THEN 'Giám sát SOC'
        WHEN 'attt_httt_dau_tu' THEN 'An toàn thông tin đầu tư'
        WHEN 'attt_httt_van_hanh' THEN 'An toàn thông tin vận hành'
        WHEN 'giai_phap_attt' THEN 'Giải pháp ATTT'
        WHEN 'thong_bao' THEN 'Thông báo'
        WHEN 'files' THEN 'Tệp tin'
        WHEN 'auth' THEN 'Xác thực'
        WHEN 'system' THEN 'Hệ thống'
        ELSE INITCAP(REPLACE(LOWER(TRIM(TO_CHAR(p.MODULE))), '_', ' '))
      END
    )
    || ' : ' ||
    (
      CASE LOWER(TRIM(TO_CHAR(p.ACTION)))
        WHEN 'read' THEN 'Xem'
        WHEN 'create' THEN 'Thêm'
        WHEN 'update' THEN 'Sửa'
        WHEN 'delete' THEN 'Xóa'
        WHEN 'approve' THEN 'Phê duyệt'
        WHEN 'submit' THEN 'Gửi'
        WHEN 'upload' THEN 'Tải lên'
        WHEN 'export' THEN 'Xuất'
        WHEN 'pdf' THEN 'Xuất PDF'
        WHEN 'admin' THEN 'Quản trị'
        WHEN 'me' THEN 'Thông tin của tôi'
        ELSE INITCAP(REPLACE(LOWER(TRIM(TO_CHAR(p.ACTION))), '_', ' '))
      END
    );
