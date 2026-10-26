-- V93: Tong quat hoa cau hinh danh muc theo don vi.
-- V92 da duoc phat hanh, vi vay khong sua checksum V92; migration nay
-- chuyen ca cau truc va du lieu cu sang khoa danh muc mo.

DECLARE
    v_old_table NUMBER;
    v_new_table NUMBER;
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_old_table
    FROM USER_TABLES WHERE TABLE_NAME = 'REF_DANH_MUC_DON_VI';
    SELECT COUNT(*) INTO v_new_table
    FROM USER_TABLES WHERE TABLE_NAME = 'CATALOG_UNIT_SETTINGS';

    -- Nhanh nay chay tren database moi ap dung V92.
    IF v_old_table > 0 AND v_new_table = 0 THEN
        SELECT COUNT(*) INTO v_count
        FROM USER_INDEXES WHERE INDEX_NAME = 'IX_DMDV_LOOKUP';
        IF v_count > 0 THEN
            EXECUTE IMMEDIATE 'DROP INDEX IX_DMDV_LOOKUP';
        END IF;

        SELECT COUNT(*) INTO v_count
        FROM USER_CONSTRAINTS WHERE CONSTRAINT_NAME = 'UQ_DMDV_ITEM';
        IF v_count > 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE REF_DANH_MUC_DON_VI DROP CONSTRAINT UQ_DMDV_ITEM';
        END IF;

        SELECT COUNT(*) INTO v_count
        FROM USER_CONSTRAINTS WHERE CONSTRAINT_NAME = 'CK_DMDV_LOAI';
        IF v_count > 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE REF_DANH_MUC_DON_VI DROP CONSTRAINT CK_DMDV_LOAI';
        END IF;

        EXECUTE IMMEDIATE 'ALTER TABLE REF_DANH_MUC_DON_VI MODIFY (DANH_MUC_KEY NVARCHAR2(150))';
        EXECUTE IMMEDIATE q'[
            UPDATE REF_DANH_MUC_DON_VI
            SET DANH_MUC_KEY = CASE
                WHEN DANH_MUC_LOAI = N'CODE' THEN N'CODE:' || DANH_MUC_KEY
                WHEN DANH_MUC_LOAI = N'DEVICE' THEN N'REF:' || DANH_MUC_KEY
                ELSE DANH_MUC_LOAI || N':' || DANH_MUC_KEY
            END
        ]';
        EXECUTE IMMEDIATE 'ALTER TABLE REF_DANH_MUC_DON_VI DROP COLUMN DANH_MUC_LOAI';
        EXECUTE IMMEDIATE 'ALTER TABLE REF_DANH_MUC_DON_VI RENAME COLUMN DANH_MUC_KEY TO CATALOG_KEY';
        EXECUTE IMMEDIATE 'ALTER TABLE REF_DANH_MUC_DON_VI RENAME TO CATALOG_UNIT_SETTINGS';
    END IF;

    -- Cac buoc sau cung cho phep chay tiep neu Oracle da auto-commit mot phan DDL.
    SELECT COUNT(*) INTO v_count
    FROM USER_CONSTRAINTS WHERE CONSTRAINT_NAME = 'FK_DMDV_DON_VI';
    IF v_count > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE CATALOG_UNIT_SETTINGS RENAME CONSTRAINT FK_DMDV_DON_VI TO FK_CUS_DON_VI';
    END IF;

    SELECT COUNT(*) INTO v_count
    FROM USER_CONSTRAINTS WHERE CONSTRAINT_NAME = 'CK_DMDV_VISIBLE';
    IF v_count > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE CATALOG_UNIT_SETTINGS RENAME CONSTRAINT CK_DMDV_VISIBLE TO CK_CUS_VISIBLE';
    END IF;

    SELECT COUNT(*) INTO v_count
    FROM USER_CONSTRAINTS WHERE CONSTRAINT_NAME = 'UQ_CATALOG_UNIT_ITEM';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE CATALOG_UNIT_SETTINGS ADD CONSTRAINT UQ_CATALOG_UNIT_ITEM UNIQUE (DON_VI_ID, CATALOG_KEY, ITEM_ID)';
    END IF;

    SELECT COUNT(*) INTO v_count
    FROM USER_INDEXES WHERE INDEX_NAME = 'IX_CATALOG_UNIT_LOOKUP';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IX_CATALOG_UNIT_LOOKUP ON CATALOG_UNIT_SETTINGS (DON_VI_ID, CATALOG_KEY)';
    END IF;
END;
/

-- Tao quyen moi neu seeder chua tao. Neu seeder da chay thi giu nguyen ban ghi.
INSERT INTO IDM_PERMISSIONS (ID, PERM_CODE, MODULE, ACTION, MO_TA)
SELECT
    (SELECT COALESCE(MAX(ID), 3000) + 1 FROM IDM_PERMISSIONS),
    'catalog_customization:read',
    'catalog_customization',
    'read',
    'Tùy chỉnh danh mục theo đơn vị : Xem'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM IDM_PERMISSIONS WHERE PERM_CODE = 'catalog_customization:read'
);

INSERT INTO IDM_PERMISSIONS (ID, PERM_CODE, MODULE, ACTION, MO_TA)
SELECT
    (SELECT COALESCE(MAX(ID), 3000) + 1 FROM IDM_PERMISSIONS),
    'catalog_customization:update',
    'catalog_customization',
    'update',
    'Tùy chỉnh danh mục theo đơn vị : Cập nhật'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM IDM_PERMISSIONS WHERE PERM_CODE = 'catalog_customization:update'
);

-- Bao toan cac gan quyen da co tu V92, ke ca khi role duoc cau hinh rieng.
INSERT INTO IDM_ROLE_PERMISSIONS (ROLE_ID, PERMISSION_ID)
SELECT old_rp.ROLE_ID, new_p.ID
FROM IDM_ROLE_PERMISSIONS old_rp
JOIN IDM_PERMISSIONS old_p ON old_p.ID = old_rp.PERMISSION_ID
JOIN IDM_PERMISSIONS new_p
  ON (old_p.PERM_CODE = N'danh_muc_don_vi:read' AND new_p.PERM_CODE = N'catalog_customization:read')
  OR (old_p.PERM_CODE = N'danh_muc_don_vi:update' AND new_p.PERM_CODE = N'catalog_customization:update')
WHERE old_p.PERM_CODE IN ('danh_muc_don_vi:read', 'danh_muc_don_vi:update')
  AND NOT EXISTS (
      SELECT 1
      FROM IDM_ROLE_PERMISSIONS existing_rp
      WHERE existing_rp.ROLE_ID = old_rp.ROLE_ID
        AND existing_rp.PERMISSION_ID = new_p.ID
  );

DELETE FROM IDM_ROLE_PERMISSIONS
WHERE PERMISSION_ID IN (
    SELECT ID
    FROM IDM_PERMISSIONS
    WHERE PERM_CODE IN ('danh_muc_don_vi:read', 'danh_muc_don_vi:update')
);

DELETE FROM IDM_PERMISSIONS
WHERE PERM_CODE IN ('danh_muc_don_vi:read', 'danh_muc_don_vi:update');
