DECLARE
    v_column_count NUMBER;
    v_index_count NUMBER;
    v_nullable VARCHAR2(1);

    PROCEDURE drop_index_if_exists(p_index_name IN VARCHAR2) IS
    BEGIN
        SELECT COUNT(1)
          INTO v_index_count
          FROM USER_INDEXES
         WHERE INDEX_NAME = UPPER(p_index_name);

        IF v_index_count > 0 THEN
            EXECUTE IMMEDIATE 'DROP INDEX ' || p_index_name;
        END IF;
    END;

    PROCEDURE add_column_if_missing(
        p_table_name IN VARCHAR2,
        p_column_name IN VARCHAR2,
        p_definition IN VARCHAR2
    ) IS
    BEGIN
        SELECT COUNT(1)
          INTO v_column_count
          FROM USER_TAB_COLS
         WHERE TABLE_NAME = UPPER(p_table_name)
           AND COLUMN_NAME = UPPER(p_column_name);

        IF v_column_count = 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE ' || p_table_name || ' ADD (' || p_column_name || ' ' || p_definition || ')';
        END IF;
    END;

    PROCEDURE drop_column_if_exists(
        p_table_name IN VARCHAR2,
        p_column_name IN VARCHAR2
    ) IS
    BEGIN
        SELECT COUNT(1)
          INTO v_column_count
          FROM USER_TAB_COLS
         WHERE TABLE_NAME = UPPER(p_table_name)
           AND COLUMN_NAME = UPPER(p_column_name);

        IF v_column_count > 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE ' || p_table_name || ' DROP COLUMN ' || p_column_name;
        END IF;
    END;

    PROCEDURE ensure_not_null(
        p_table_name IN VARCHAR2,
        p_column_name IN VARCHAR2,
        p_definition IN VARCHAR2
    ) IS
    BEGIN
        SELECT NULLABLE
          INTO v_nullable
          FROM USER_TAB_COLS
         WHERE TABLE_NAME = UPPER(p_table_name)
           AND COLUMN_NAME = UPPER(p_column_name);

        IF v_nullable = 'Y' THEN
            EXECUTE IMMEDIATE 'ALTER TABLE ' || p_table_name || ' MODIFY (' || p_column_name || ' ' || p_definition || ' NOT NULL)';
        END IF;
    END;
BEGIN
    DELETE FROM BIZ_GIAM_SAT_SOC;
    DELETE FROM BIZ_GIAM_SAT_NOC;

    drop_index_if_exists('IX_BIZ_GSS_DV_KY');
    drop_index_if_exists('IX_BIZ_GSN_DV_KY');
    drop_index_if_exists('UQ_BIZ_GSS_SCOPE');
    drop_index_if_exists('UQ_BIZ_GSN_SCOPE');

    add_column_if_missing('BIZ_GIAM_SAT_SOC', 'LOAI_MANG', 'NVARCHAR2(50)');
    add_column_if_missing('BIZ_GIAM_SAT_SOC', 'LOP_GIAM_SAT', 'NVARCHAR2(50)');
    add_column_if_missing('BIZ_GIAM_SAT_SOC', 'KY_BAO_CAO_CODE', 'NVARCHAR2(50)');
    add_column_if_missing('BIZ_GIAM_SAT_SOC', 'CO_HE_THONG', 'NUMBER(1) DEFAULT 0 NOT NULL');
    add_column_if_missing('BIZ_GIAM_SAT_SOC', 'THUC_TRANG', 'NVARCHAR2(50)');

    add_column_if_missing('BIZ_GIAM_SAT_NOC', 'KY_BAO_CAO_CODE', 'NVARCHAR2(50)');
    add_column_if_missing('BIZ_GIAM_SAT_NOC', 'LOP_GIAM_SAT', 'NVARCHAR2(50)');
    add_column_if_missing('BIZ_GIAM_SAT_NOC', 'THUC_TRANG', 'NVARCHAR2(50)');

    EXECUTE IMMEDIATE 'UPDATE BIZ_GIAM_SAT_SOC SET KY_BAO_CAO_CODE = NVL(KY_BAO_CAO_CODE, ''2025Q4_ATTT'')';
    EXECUTE IMMEDIATE 'UPDATE BIZ_GIAM_SAT_SOC SET LOAI_MANG = NVL(LOAI_MANG, ''MANG_NOI_BO''), LOP_GIAM_SAT = NVL(LOP_GIAM_SAT, ''LOP_MANG'')';
    EXECUTE IMMEDIATE 'UPDATE BIZ_GIAM_SAT_NOC SET KY_BAO_CAO_CODE = NVL(KY_BAO_CAO_CODE, ''2025Q4_ATTT'')';
    EXECUTE IMMEDIATE 'UPDATE BIZ_GIAM_SAT_NOC SET LOP_GIAM_SAT = NVL(LOP_GIAM_SAT, ''LOP_MANG'')';

    ensure_not_null('BIZ_GIAM_SAT_SOC', 'KY_BAO_CAO_CODE', 'NVARCHAR2(50)');
    ensure_not_null('BIZ_GIAM_SAT_SOC', 'LOAI_MANG', 'NVARCHAR2(50)');
    ensure_not_null('BIZ_GIAM_SAT_SOC', 'LOP_GIAM_SAT', 'NVARCHAR2(50)');
    ensure_not_null('BIZ_GIAM_SAT_NOC', 'KY_BAO_CAO_CODE', 'NVARCHAR2(50)');
    ensure_not_null('BIZ_GIAM_SAT_NOC', 'LOP_GIAM_SAT', 'NVARCHAR2(50)');

        SELECT COUNT(1)
            INTO v_column_count
            FROM USER_TAB_COLS
         WHERE TABLE_NAME = 'BIZ_GIAM_SAT_SOC'
             AND COLUMN_NAME = 'CO_SOC';

        IF v_column_count > 0 THEN
                EXECUTE IMMEDIATE 'UPDATE BIZ_GIAM_SAT_SOC SET CO_HE_THONG = NVL(CO_HE_THONG, CO_SOC)';
        END IF;

    drop_column_if_exists('BIZ_GIAM_SAT_SOC', 'CO_SOC');
    drop_column_if_exists('BIZ_GIAM_SAT_NOC', 'PHAM_VI_GIAM_SAT');

    EXECUTE IMMEDIATE 'CREATE UNIQUE INDEX UQ_BIZ_GSS_SCOPE ON BIZ_GIAM_SAT_SOC (DON_VI_ID, KY_BAO_CAO_CODE, LOAI_MANG, LOP_GIAM_SAT)';
    EXECUTE IMMEDIATE 'CREATE UNIQUE INDEX UQ_BIZ_GSN_SCOPE ON BIZ_GIAM_SAT_NOC (DON_VI_ID, KY_BAO_CAO_CODE, LOP_GIAM_SAT)';
END;
/