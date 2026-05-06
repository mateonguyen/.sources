-- V24: Rename ORG_DON_VI -> REF_DON_VI, drop ORG_LOAI_DON_VI, redesign BIZ_HA_TANG_MANG

DECLARE
    v_org_count NUMBER;
    v_ref_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_org_count FROM USER_TABLES WHERE TABLE_NAME = 'ORG_DON_VI';
    SELECT COUNT(*) INTO v_ref_count FROM USER_TABLES WHERE TABLE_NAME = 'REF_DON_VI';

    IF v_org_count > 0 AND v_ref_count = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE ORG_DON_VI RENAME TO REF_DON_VI';
    END IF;
END;
/

DECLARE
    v_constraint_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_constraint_count
    FROM USER_CONSTRAINTS
    WHERE TABLE_NAME = 'REF_DON_VI'
      AND CONSTRAINT_NAME = 'FK_DON_VI_LOAI';

    IF v_constraint_count > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE REF_DON_VI DROP CONSTRAINT FK_DON_VI_LOAI';
    END IF;
END;
/

DECLARE
    v_column_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_column_count
    FROM USER_TAB_COLS
    WHERE TABLE_NAME = 'REF_DON_VI'
      AND COLUMN_NAME = 'LOAI_DON_VI_ID';

    IF v_column_count > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE REF_DON_VI DROP COLUMN LOAI_DON_VI_ID';
    END IF;
END;
/

DECLARE
    v_table_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_table_count FROM USER_TABLES WHERE TABLE_NAME = 'ORG_LOAI_DON_VI';

    IF v_table_count > 0 THEN
        EXECUTE IMMEDIATE 'DROP TABLE ORG_LOAI_DON_VI';
    END IF;
END;
/

DELETE FROM BIZ_HA_TANG_MANG;

DECLARE
    v_index_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_index_count FROM USER_INDEXES WHERE INDEX_NAME = 'IX_BIZ_HTM_DV_KY';

    IF v_index_count > 0 THEN
        EXECUTE IMMEDIATE 'DROP INDEX IX_BIZ_HTM_DV_KY';
    END IF;
END;
/

DECLARE
    PROCEDURE drop_column_if_exists(p_table_name VARCHAR2, p_column_name VARCHAR2) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_count
        FROM USER_TAB_COLS
        WHERE TABLE_NAME = p_table_name
          AND COLUMN_NAME = p_column_name;

        IF v_count > 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE ' || p_table_name || ' DROP COLUMN ' || p_column_name;
        END IF;
    END;
BEGIN
    drop_column_if_exists('BIZ_HA_TANG_MANG', 'KY_BAO_CAO_CODE');
    drop_column_if_exists('BIZ_HA_TANG_MANG', 'LOAI_KET_NOI');
    drop_column_if_exists('BIZ_HA_TANG_MANG', 'BANG_THONG');
    drop_column_if_exists('BIZ_HA_TANG_MANG', 'NHA_CUNG_CAP');
    drop_column_if_exists('BIZ_HA_TANG_MANG', 'SO_LUONG_DIEM');
    drop_column_if_exists('BIZ_HA_TANG_MANG', 'TINH_TRANG');
END;
/

DECLARE
    PROCEDURE add_column_if_missing(p_statement VARCHAR2, p_column_name VARCHAR2) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_count
        FROM USER_TAB_COLS
        WHERE TABLE_NAME = 'BIZ_HA_TANG_MANG'
          AND COLUMN_NAME = p_column_name;

        IF v_count = 0 THEN
            EXECUTE IMMEDIATE p_statement;
        END IF;
    END;
BEGIN
    add_column_if_missing('ALTER TABLE BIZ_HA_TANG_MANG ADD (LOAI_DV_THONG_KE NVARCHAR2(30) DEFAULT ''CAP_PHONG_BO'' NOT NULL)', 'LOAI_DV_THONG_KE');
    add_column_if_missing('ALTER TABLE BIZ_HA_TANG_MANG ADD (SO_DON_VI_TRUC_THUOC NUMBER(10) DEFAULT 0 NOT NULL)', 'SO_DON_VI_TRUC_THUOC');
    add_column_if_missing('ALTER TABLE BIZ_HA_TANG_MANG ADD (SO_DA_KET_NOI_BCANET NUMBER(10) DEFAULT 0 NOT NULL)', 'SO_DA_KET_NOI_BCANET');
    add_column_if_missing('ALTER TABLE BIZ_HA_TANG_MANG ADD (SO_DUONG_TRUYEN_VNPT NUMBER(10) DEFAULT 0 NOT NULL)', 'SO_DUONG_TRUYEN_VNPT');
    add_column_if_missing('ALTER TABLE BIZ_HA_TANG_MANG ADD (SO_DUONG_TRUYEN_KHAC NUMBER(10) DEFAULT 0 NOT NULL)', 'SO_DUONG_TRUYEN_KHAC');
    add_column_if_missing('ALTER TABLE BIZ_HA_TANG_MANG ADD (SO_KET_NOI_INTERNET NUMBER(10) DEFAULT 0 NOT NULL)', 'SO_KET_NOI_INTERNET');
END;
/

DECLARE
    v_index_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_index_count FROM USER_INDEXES WHERE INDEX_NAME = 'IX_BIZ_HTM_DV';

    IF v_index_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IX_BIZ_HTM_DV ON BIZ_HA_TANG_MANG (DON_VI_ID)';
    END IF;
END;
/

DECLARE
    v_index_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_index_count FROM USER_INDEXES WHERE INDEX_NAME = 'UX_BIZ_HTM_DV_LOAI';

    IF v_index_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE UNIQUE INDEX UX_BIZ_HTM_DV_LOAI ON BIZ_HA_TANG_MANG (DON_VI_ID, LOAI_DV_THONG_KE)';
    END IF;
END;
/