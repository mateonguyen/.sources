-- Khôi phục phân nhóm PM/CSDL theo đúng biểu mẫu:
--   DUNG_CHUNG     : do các Cục nghiệp vụ triển khai dùng chung
--   TU_PHAT_TRIEN  : do đơn vị, địa phương tự nghiên cứu/phát triển
-- Dữ liệu cũ không còn thông tin phân loại sau V64 được đánh dấu CHUA_PHAN_LOAI,
-- tuyệt đối không suy đoán để tránh xuất sai tiểu mục báo cáo.

DECLARE
    v_index_count NUMBER;

    PROCEDURE add_column_if_missing(
        p_table_name IN VARCHAR2,
        p_column_name IN VARCHAR2
    ) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(1)
          INTO v_count
          FROM USER_TAB_COLS
         WHERE TABLE_NAME = UPPER(p_table_name)
           AND COLUMN_NAME = UPPER(p_column_name);

        IF v_count = 0 THEN
            EXECUTE IMMEDIATE
                'ALTER TABLE ' || p_table_name ||
                ' ADD (' || p_column_name ||
                ' NVARCHAR2(20) DEFAULT ''CHUA_PHAN_LOAI'' NOT NULL)';
        END IF;
    END;

    PROCEDURE add_check_if_missing(
        p_table_name IN VARCHAR2,
        p_constraint_name IN VARCHAR2
    ) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(1)
          INTO v_count
          FROM USER_CONSTRAINTS
         WHERE CONSTRAINT_NAME = UPPER(p_constraint_name);

        IF v_count = 0 THEN
            EXECUTE IMMEDIATE
                'ALTER TABLE ' || p_table_name ||
                ' ADD CONSTRAINT ' || p_constraint_name ||
                ' CHECK (LOAI_PHAN_MEM IN (''DUNG_CHUNG'', ''TU_PHAT_TRIEN'', ''CHUA_PHAN_LOAI''))';
        END IF;
    END;

BEGIN
    add_column_if_missing('BIZ_HE_THONG_THONG_TIN', 'LOAI_PHAN_MEM');
    add_column_if_missing('BIZ_HE_THONG_THONG_TIN_HIS', 'LOAI_PHAN_MEM');

    add_check_if_missing('BIZ_HE_THONG_THONG_TIN', 'CK_BIZ_HTTT_LOAI_PM');
    add_check_if_missing('BIZ_HE_THONG_THONG_TIN_HIS', 'CK_HTTT_HIS_LOAI_PM');

    SELECT COUNT(1)
      INTO v_index_count
      FROM USER_INDEXES
     WHERE INDEX_NAME = 'IX_BIZ_HTTT_DV_LOAI';

    IF v_index_count = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE INDEX IX_BIZ_HTTT_DV_LOAI ON BIZ_HE_THONG_THONG_TIN (DON_VI_ID, LOAI_PHAN_MEM)';
    END IF;
END;
/
