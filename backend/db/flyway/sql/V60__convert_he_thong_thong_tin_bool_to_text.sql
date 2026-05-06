-- V27: Convert UngDungCnMoi and KhaNangTichHop from NUMBER(1) to NVARCHAR2, add PhamViHoatDongKyThuat

DECLARE
    v_col_exists NUMBER;

    PROCEDURE drop_column_if_exists(p_table_name IN VARCHAR2, p_column_name IN VARCHAR2) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(1)
        INTO v_count
        FROM USER_TAB_COLS
        WHERE TABLE_NAME = UPPER(p_table_name)
          AND COLUMN_NAME = UPPER(p_column_name);

        IF v_count > 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE ' || p_table_name || ' DROP COLUMN ' || p_column_name;
        END IF;
    END;

    PROCEDURE add_column_if_missing(
        p_table_name IN VARCHAR2,
        p_column_name IN VARCHAR2,
        p_column_ddl IN VARCHAR2
    ) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(1)
        INTO v_count
        FROM USER_TAB_COLS
        WHERE TABLE_NAME = UPPER(p_table_name)
          AND COLUMN_NAME = UPPER(p_column_name);

        IF v_count = 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE ' || p_table_name || ' ADD (' || p_column_ddl || ')';
        END IF;
    END;

BEGIN
    -- Update BIZ_HE_THONG_THONG_TIN: Convert bool columns to text and add new column
    drop_column_if_exists('BIZ_HE_THONG_THONG_TIN', 'UNG_DUNG_CN_MOI');
    drop_column_if_exists('BIZ_HE_THONG_THONG_TIN', 'KHA_NANG_TICH_HOP');

    add_column_if_missing('BIZ_HE_THONG_THONG_TIN', 'UNG_DUNG_CN_MOI', 'UNG_DUNG_CN_MOI NVARCHAR2(1000) NULL');
    add_column_if_missing('BIZ_HE_THONG_THONG_TIN', 'KHA_NANG_TICH_HOP', 'KHA_NANG_TICH_HOP NVARCHAR2(1000) NULL');
    add_column_if_missing('BIZ_HE_THONG_THONG_TIN', 'PHAM_VI_HOAT_DONG_KY_THUAT', 'PHAM_VI_HOAT_DONG_KY_THUAT NVARCHAR2(500) NULL');

    -- Update BIZ_HE_THONG_THONG_TIN_HIS: Convert bool columns to text and add new column
    drop_column_if_exists('BIZ_HE_THONG_THONG_TIN_HIS', 'UNG_DUNG_CN_MOI');
    drop_column_if_exists('BIZ_HE_THONG_THONG_TIN_HIS', 'KHA_NANG_TICH_HOP');

    add_column_if_missing('BIZ_HE_THONG_THONG_TIN_HIS', 'UNG_DUNG_CN_MOI', 'UNG_DUNG_CN_MOI NVARCHAR2(1000) NULL');
    add_column_if_missing('BIZ_HE_THONG_THONG_TIN_HIS', 'KHA_NANG_TICH_HOP', 'KHA_NANG_TICH_HOP NVARCHAR2(1000) NULL');
    add_column_if_missing('BIZ_HE_THONG_THONG_TIN_HIS', 'PHAM_VI_HOAT_DONG_KY_THUAT', 'PHAM_VI_HOAT_DONG_KY_THUAT NVARCHAR2(500) NULL');

END;
/
