-- V19: Xoa cac cot khong con su dung tren BIZ_NHAN_LUC_CNTT

BEGIN
    DECLARE
        v_count NUMBER := 0;
    BEGIN
        SELECT COUNT(1)
          INTO v_count
          FROM USER_TAB_COLS
         WHERE TABLE_NAME = 'BIZ_NHAN_LUC_CNTT'
           AND COLUMN_NAME = 'CHUYEN_NGANH';

        IF v_count > 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE BIZ_NHAN_LUC_CNTT DROP COLUMN CHUYEN_NGANH';
        END IF;

        SELECT COUNT(1)
          INTO v_count
          FROM USER_TAB_COLS
         WHERE TABLE_NAME = 'BIZ_NHAN_LUC_CNTT'
           AND COLUMN_NAME = 'NAM_KINH_NGHIEM';

        IF v_count > 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE BIZ_NHAN_LUC_CNTT DROP COLUMN NAM_KINH_NGHIEM';
        END IF;
    END;
END;
/