-- V37: Refactor DaoTaoHocVien và DaoTaoNuocNgoai sang mô hình period-independent
--
-- DaoTaoHocVien: Thêm cột NAM (năm) thay cho KY_BAO_CAO_CODE đã bị drop ở V36.
--   Unique: (DON_VI_ID, NAM, NOI_DUNG_DAO_TAO) — mỗi loại nội dung 1 bản ghi/năm/đơn vị.
--
-- DaoTaoNuocNgoai: Drop KY_BAO_CAO_CODE. Dữ liệu đã có THOI_GIAN_TU/DEN xác định năm.

DECLARE
    v_count NUMBER;
BEGIN

    -- ===== BIZ_DAO_TAO_HOC_VIEN: thêm cột NAM =====
    SELECT COUNT(*) INTO v_count FROM USER_TAB_COLUMNS
    WHERE TABLE_NAME = 'BIZ_DAO_TAO_HOC_VIEN' AND COLUMN_NAME = 'NAM';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE BIZ_DAO_TAO_HOC_VIEN ADD (NAM NUMBER(4) DEFAULT EXTRACT(YEAR FROM SYSDATE) NOT NULL)';
    END IF;

    -- Index theo đơn vị + năm
    SELECT COUNT(*) INTO v_count FROM USER_INDEXES WHERE INDEX_NAME = 'IX_BIZ_DTHV_DV_NAM';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IX_BIZ_DTHV_DV_NAM ON BIZ_DAO_TAO_HOC_VIEN (DON_VI_ID, NAM)';
    END IF;

    -- Unique: mỗi loại nội dung đào tạo chỉ 1 bản ghi / đơn vị / năm
    SELECT COUNT(*) INTO v_count FROM USER_INDEXES WHERE INDEX_NAME = 'UX_BIZ_DTHV_DV_NAM_ND';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE UNIQUE INDEX UX_BIZ_DTHV_DV_NAM_ND ON BIZ_DAO_TAO_HOC_VIEN (DON_VI_ID, NAM, NOI_DUNG_DAO_TAO)';
    END IF;

    -- ===== BIZ_DAO_TAO_NUOC_NGOAI: drop KY_BAO_CAO_CODE =====
    SELECT COUNT(*) INTO v_count FROM USER_TAB_COLUMNS
    WHERE TABLE_NAME = 'BIZ_DAO_TAO_NUOC_NGOAI' AND COLUMN_NAME = 'KY_BAO_CAO_CODE';
    IF v_count > 0 THEN
        -- Drop index trước nếu tồn tại
        SELECT COUNT(*) INTO v_count FROM USER_INDEXES WHERE INDEX_NAME = 'IX_BIZ_DTNN_DV_KY';
        IF v_count > 0 THEN
            EXECUTE IMMEDIATE 'DROP INDEX IX_BIZ_DTNN_DV_KY';
        END IF;

        EXECUTE IMMEDIATE 'ALTER TABLE BIZ_DAO_TAO_NUOC_NGOAI DROP COLUMN KY_BAO_CAO_CODE';
    END IF;

END;
/
COMMIT;
