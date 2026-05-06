-- V62: rename MA_HE_THONG -> TEN_HE_THONG and increase length for HTTT standard tables

DECLARE
    PROCEDURE rename_column_if_needed(
        p_table_name  IN VARCHAR2,
        p_old_name    IN VARCHAR2,
        p_new_name    IN VARCHAR2
    ) IS
        v_old_count NUMBER;
        v_new_count NUMBER;
    BEGIN
        SELECT COUNT(*)
          INTO v_old_count
          FROM USER_TAB_COLUMNS
         WHERE TABLE_NAME = UPPER(p_table_name)
           AND COLUMN_NAME = UPPER(p_old_name);

        SELECT COUNT(*)
          INTO v_new_count
          FROM USER_TAB_COLUMNS
         WHERE TABLE_NAME = UPPER(p_table_name)
           AND COLUMN_NAME = UPPER(p_new_name);

        IF v_old_count > 0 AND v_new_count = 0 THEN
            EXECUTE IMMEDIATE
                'ALTER TABLE ' || p_table_name ||
                ' RENAME COLUMN ' || p_old_name ||
                ' TO ' || p_new_name;
        END IF;
    END;

    PROCEDURE alter_column_length(
        p_table_name   IN VARCHAR2,
        p_column_name  IN VARCHAR2,
        p_length       IN NUMBER
    ) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*)
          INTO v_count
          FROM USER_TAB_COLUMNS
         WHERE TABLE_NAME = UPPER(p_table_name)
           AND COLUMN_NAME = UPPER(p_column_name);

        IF v_count > 0 THEN
            EXECUTE IMMEDIATE
                'ALTER TABLE ' || p_table_name ||
                ' MODIFY ' || p_column_name || ' NVARCHAR2(' || p_length || ')';
        END IF;
    END;
BEGIN
    rename_column_if_needed('BIZ_HTTT_TIEU_CHUAN', 'MA_HE_THONG', 'TEN_HE_THONG');
    rename_column_if_needed('BIZ_HTTT_TIEU_CHUAN_HIS', 'MA_HE_THONG', 'TEN_HE_THONG');

    alter_column_length('BIZ_HTTT_TIEU_CHUAN', 'TEN_HE_THONG', 1000);
    alter_column_length('BIZ_HTTT_TIEU_CHUAN_HIS', 'TEN_HE_THONG', 1000);
END;
/
