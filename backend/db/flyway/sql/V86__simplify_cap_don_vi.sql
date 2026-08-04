-- Rut gon CAP_DON_VI tu 6 gia tri (BO/CUC/TINH/PHONG/XA/HOC_VIEN) xuong con
-- 3 gia tri dai dien dung 3 tang cay: CAP_0 (goc - Bo), CAP_1 (truc thuoc
-- Bo - Cuc/Tinh/Hoc vien), CAP_2 (truc thuoc Cuc/Tinh - Phong/Xa).
-- Chua co du lieu REF_DON_VI that nao dung 6 gia tri cu (CAP_DON_VI con
-- NULL tren toan bo don vi import tu Thong tu 58) nen an toan de xoa thang.
DELETE FROM CODE_VALUES
WHERE CODE_ID = (SELECT ID FROM CODES WHERE CODE = 'CAP_DON_VI')
  AND VALUE IN ('BO', 'CUC', 'TINH', 'PHONG', 'XA', 'HOC_VIEN');
