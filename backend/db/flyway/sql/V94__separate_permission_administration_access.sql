-- V94: Tach quyen xem danh muc quyen va quan ly phan quyen khoi quyen vai tro.
-- Muc tieu: role quan ly van co the quan ly vai tro neu duoc cap roles:*, nhung
-- khong duoc xem danh muc quyen, gan vai tro hay gan quyen neu khong co quyen rieng.

INSERT INTO IDM_PERMISSIONS (ID, PERM_CODE, MODULE, ACTION, MO_TA)
SELECT
    (SELECT COALESCE(MAX(ID), 3000) + 1 FROM IDM_PERMISSIONS),
    'permissions:read',
    'permissions',
    'read',
    'Quyền : Xem'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM IDM_PERMISSIONS WHERE PERM_CODE = 'permissions:read'
);

INSERT INTO IDM_PERMISSIONS (ID, PERM_CODE, MODULE, ACTION, MO_TA)
SELECT
    (SELECT COALESCE(MAX(ID), 3000) + 1 FROM IDM_PERMISSIONS),
    'phan_quyen:read',
    'phan_quyen',
    'read',
    'Phân quyền : Xem'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM IDM_PERMISSIONS WHERE PERM_CODE = 'phan_quyen:read'
);

INSERT INTO IDM_PERMISSIONS (ID, PERM_CODE, MODULE, ACTION, MO_TA)
SELECT
    (SELECT COALESCE(MAX(ID), 3000) + 1 FROM IDM_PERMISSIONS),
    'phan_quyen:update',
    'phan_quyen',
    'update',
    'Phân quyền : Sửa'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM IDM_PERMISSIONS WHERE PERM_CODE = 'phan_quyen:update'
);

-- Quyen xoa ky da duoc controller su dung nhung bi thieu trong danh muc quyen,
-- nen truoc V94 khong the gan qua man hinh Phan quyen.
INSERT INTO IDM_PERMISSIONS (ID, PERM_CODE, MODULE, ACTION, MO_TA)
SELECT
    (SELECT COALESCE(MAX(ID), 3000) + 1 FROM IDM_PERMISSIONS),
    'ky_bao_cao:delete',
    'ky_bao_cao',
    'delete',
    'Kỳ báo cáo : Xóa'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM IDM_PERMISSIONS WHERE PERM_CODE = 'ky_bao_cao:delete'
);

-- Cac quyen nay chi danh cho quan tri he thong. SYSTEM_ADMIN duoc uy quyen qua
-- system:admin; xoa moi gan truc tiep cho cac role khac neu moi truong da tung
-- duoc seed/thu nghiem voi cac ma quyen tren.
DELETE FROM IDM_ROLE_PERMISSIONS rp
WHERE rp.PERMISSION_ID IN (
    SELECT p.ID
    FROM IDM_PERMISSIONS p
    WHERE p.PERM_CODE IN (
        'permissions:read',
        'phan_quyen:read',
        'phan_quyen:update'
    )
)
AND rp.ROLE_ID IN (
    SELECT r.ID
    FROM IDM_ROLES r
    WHERE NVL(UPPER(r.NAME), '#') <> 'SYSTEM_ADMIN'
);

-- Hai ma admin cu khong con bao ve endpoint nao; thao tac gan vai tro/quyen da
-- duoc chuyen sang phan_quyen:update de hien thi va quan ly ro rang trong ma tran.
DELETE FROM IDM_ROLE_PERMISSIONS
WHERE PERMISSION_ID IN (
    SELECT ID FROM IDM_PERMISSIONS WHERE PERM_CODE IN ('users:admin', 'roles:admin')
);

DELETE FROM IDM_PERMISSIONS
WHERE PERM_CODE IN ('users:admin', 'roles:admin');
