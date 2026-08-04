-- V83: Cap quyen don_vi:read cho role CAP_XA.
-- Bug: cac trang module nghiep vu (Nhan luc CNTT, Thiet bi CNTT, He thong thong tin,
-- Ha tang mang, ATTT van hanh...) goi donViApi.getTree()/getById() khong dieu kien
-- ben trong Promise.all() de dung du lieu cho combobox loc/chon don vi.
-- CAP_XA truoc day khong co don_vi:read nen moi lan vao cac module nay bi 403,
-- lam Promise.all() reject toan bo va hien hang loat toast loi "khong co quyen".
-- don_vi:read chi la thong tin ten/ma don vi (khong phai du lieu nghiep vu nhay cam),
-- cac role it quyen hon (VIEWER, LANH_DAO) da duoc cap san.

INSERT INTO IDM_ROLE_PERMISSIONS (ROLE_ID, PERMISSION_ID)
SELECT r.ID, p.ID
FROM IDM_ROLES r
JOIN IDM_PERMISSIONS p
  ON p.PERM_CODE = 'don_vi:read'
WHERE r.NORMALIZED_NAME = 'CAP_XA'
  AND NOT EXISTS (
      SELECT 1
      FROM IDM_ROLE_PERMISSIONS rp
      WHERE rp.ROLE_ID = r.ID
        AND rp.PERMISSION_ID = p.ID
  );
