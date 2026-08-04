-- V84: Cap quyen codes:read cho role CAP_XA.
-- Tiep noi bug o V83: cac trang module nghiep vu (Nhan luc CNTT, Thiet bi CNTT,
-- He thong thong tin...) con goi codesApi.getByCode(...) (GIOI_TINH, CAP_BAC_CONG_AN,
-- LOAI_NHAN_LUC, TRINH_DO_CNTT, TRINH_DO_LLCT...) khong dieu kien ben trong cung
-- mot Promise.all() voi donViApi.getTree(). CAP_XA truoc day khong co codes:read
-- nen cac request nay tra 403, Promise.all() reject toan bo, hien hang loat toast loi.
-- codes:read chi la du lieu danh muc dung chung (gioi tinh, cap bac...), khong nhay cam.

INSERT INTO IDM_ROLE_PERMISSIONS (ROLE_ID, PERMISSION_ID)
SELECT r.ID, p.ID
FROM IDM_ROLES r
JOIN IDM_PERMISSIONS p
  ON p.PERM_CODE = 'codes:read'
WHERE r.NORMALIZED_NAME = 'CAP_XA'
  AND NOT EXISTS (
      SELECT 1
      FROM IDM_ROLE_PERMISSIONS rp
      WHERE rp.ROLE_ID = r.ID
        AND rp.PERMISSION_ID = p.ID
  );
