-- V82: Tach quyen xem "Tien do bao cao" (menu /tien-do-nop) ra khoi ky_bao_cao:approve.
-- Truoc day menu nay dung chung quyen duyet/mo/dong ky bao cao (ky_bao_cao:approve),
-- trong khi ky_bao_cao:approve con duoc tai su dung o ~18 Service.cs khac nhu
-- quyen "xem du lieu khong phan biet don vi" -- khong lien quan menu nay.
-- Giu nguyen ky_bao_cao:approve cho hanh dong duyet ky; menu Tien do bao cao
-- chuyen sang dung permission rieng tien_do_bao_cao:read.

INSERT INTO IDM_PERMISSIONS (ID, PERM_CODE, MODULE, ACTION, MO_TA)
SELECT
    (SELECT COALESCE(MAX(ID), 3000) + 1 FROM IDM_PERMISSIONS),
    'tien_do_bao_cao:read',
    'tien_do_bao_cao',
    'read',
    'Tiến độ báo cáo : Xem'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM IDM_PERMISSIONS WHERE PERM_CODE = 'tien_do_bao_cao:read'
);

-- Cap quyen moi cho cac role dang co ky_bao_cao:approve (hien tai la QUAN_LY),
-- de khong mat quyen xem menu Tien do bao cao sau khi tach.
INSERT INTO IDM_ROLE_PERMISSIONS (ROLE_ID, PERMISSION_ID)
SELECT r.ID, p.ID
FROM IDM_ROLES r
JOIN IDM_ROLE_PERMISSIONS rp_approve
  ON rp_approve.ROLE_ID = r.ID
JOIN IDM_PERMISSIONS p_approve
  ON p_approve.ID = rp_approve.PERMISSION_ID
 AND p_approve.PERM_CODE = 'ky_bao_cao:approve'
JOIN IDM_PERMISSIONS p
  ON p.PERM_CODE = 'tien_do_bao_cao:read'
WHERE NOT EXISTS (
    SELECT 1
    FROM IDM_ROLE_PERMISSIONS rp
    WHERE rp.ROLE_ID = r.ID
      AND rp.PERMISSION_ID = p.ID
);
