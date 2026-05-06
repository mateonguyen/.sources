-- V41: TONG_HOP — thêm DA_XAC_NHAN cho PHONG/XA và BREAKDOWN_JSON cho Snapshot

-- 1. Flag khai báo xong của PHONG/XA (không lock dữ liệu BIZ_*)
DECLARE
    v_col_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_col_count
      FROM ALL_TAB_COLUMNS
     WHERE TABLE_NAME = 'RPT_KY_TRANG_THAI_DON_VI'
       AND COLUMN_NAME = 'DA_XAC_NHAN';
    IF v_col_count = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE RPT_KY_TRANG_THAI_DON_VI ADD DA_XAC_NHAN NUMBER(1) DEFAULT 0 NOT NULL';
    END IF;
END;
/

COMMENT ON COLUMN RPT_KY_TRANG_THAI_DON_VI.DA_XAC_NHAN
    IS '0 = chua xac nhan, 1 = da khai bao xong (khong lock BIZ_*)';

-- 2. Breakdown JSON per đơn vị con trong snapshot tổng hợp
DECLARE
    v_col_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_col_count
      FROM ALL_TAB_COLUMNS
     WHERE TABLE_NAME = 'RPT_BAO_CAO_SNAPSHOT'
       AND COLUMN_NAME = 'BREAKDOWN_JSON';
    IF v_col_count = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE RPT_BAO_CAO_SNAPSHOT ADD BREAKDOWN_JSON NVARCHAR2(2000)';
    END IF;
END;
/

COMMENT ON COLUMN RPT_BAO_CAO_SNAPSHOT.BREAKDOWN_JSON
    IS 'JSON array dong gop tung don vi con khi TONG_HOP, null khi TU_NHAP';
