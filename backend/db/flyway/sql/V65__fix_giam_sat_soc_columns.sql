-- V65: Fix BIZ_GIAM_SAT_SOC — xoa truong sai, them truong dung theo bieu mau so 8 (SOC)
--
-- Truong SAI (tu mo hinh V10 cu — tao SOC nhu phong ban, khong phai matrix giam sat):
--   NAM_THANH_LAP, SO_NHAN_SU, CONG_CU_SU_DUNG, SO_CANH_BAO_THANG
--
-- Truong THIEU theo bieu mau so 8, cot 6-12:
--   TONG_SO_DOI_TUONG    (col 6)  — Tong so doi tuong
--   SO_GIAM_SAT_MOT_PHAN (col 7)  — So doi tuong giam sat / Mot phan
--   SO_GIAM_SAT_CO_BAN   (col 8)  — So doi tuong giam sat / Co ban
--   SO_GIAM_SAT_DAY_DU   (col 9)  — So doi tuong giam sat / Day du
--   SO_SU_CO             (col 10) — So su co
--   SO_SU_CO_DA_KHAC_PHUC(col 11) — So su co da khac phuc
--   LUC_LUONG_UNG_CUU    (col 12) — Luc luong tham gia ung cuu su co (text)
--
-- Ap dung cho ca bang live va bang _HIS.

-- ═══════════════════════════════════════════════════════════════
-- PHAN 1 — BIZ_GIAM_SAT_SOC (bang live)
-- ═══════════════════════════════════════════════════════════════

-- 1a. Xoa cac truong sai
DECLARE
    PROCEDURE drop_col(p_table VARCHAR2, p_col VARCHAR2) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_count FROM USER_TAB_COLS
        WHERE TABLE_NAME = p_table AND COLUMN_NAME = p_col;
        IF v_count > 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE ' || p_table || ' DROP COLUMN ' || p_col;
        END IF;
    END;
BEGIN
    drop_col('BIZ_GIAM_SAT_SOC', 'NAM_THANH_LAP');
    drop_col('BIZ_GIAM_SAT_SOC', 'SO_NHAN_SU');
    drop_col('BIZ_GIAM_SAT_SOC', 'CONG_CU_SU_DUNG');
    drop_col('BIZ_GIAM_SAT_SOC', 'SO_CANH_BAO_THANG');
END;
/

-- 1b. Them cac truong dung theo bieu mau
DECLARE
    PROCEDURE add_col(p_ddl VARCHAR2, p_col VARCHAR2) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_count FROM USER_TAB_COLS
        WHERE TABLE_NAME = 'BIZ_GIAM_SAT_SOC' AND COLUMN_NAME = p_col;
        IF v_count = 0 THEN
            EXECUTE IMMEDIATE p_ddl;
        END IF;
    END;
BEGIN
    add_col('ALTER TABLE BIZ_GIAM_SAT_SOC ADD (TONG_SO_DOI_TUONG NUMBER(10) DEFAULT 0 NOT NULL)', 'TONG_SO_DOI_TUONG');
    add_col('ALTER TABLE BIZ_GIAM_SAT_SOC ADD (SO_GIAM_SAT_MOT_PHAN NUMBER(10) DEFAULT 0 NOT NULL)', 'SO_GIAM_SAT_MOT_PHAN');
    add_col('ALTER TABLE BIZ_GIAM_SAT_SOC ADD (SO_GIAM_SAT_CO_BAN NUMBER(10) DEFAULT 0 NOT NULL)', 'SO_GIAM_SAT_CO_BAN');
    add_col('ALTER TABLE BIZ_GIAM_SAT_SOC ADD (SO_GIAM_SAT_DAY_DU NUMBER(10) DEFAULT 0 NOT NULL)', 'SO_GIAM_SAT_DAY_DU');
    add_col('ALTER TABLE BIZ_GIAM_SAT_SOC ADD (SO_SU_CO NUMBER(10) DEFAULT 0 NOT NULL)', 'SO_SU_CO');
    add_col('ALTER TABLE BIZ_GIAM_SAT_SOC ADD (SO_SU_CO_DA_KHAC_PHUC NUMBER(10) DEFAULT 0 NOT NULL)', 'SO_SU_CO_DA_KHAC_PHUC');
    add_col('ALTER TABLE BIZ_GIAM_SAT_SOC ADD (LUC_LUONG_UNG_CUU NVARCHAR2(500) NULL)', 'LUC_LUONG_UNG_CUU');
END;
/

-- ═══════════════════════════════════════════════════════════════
-- PHAN 2 — BIZ_GIAM_SAT_SOC_HIS (bang history)
-- ═══════════════════════════════════════════════════════════════

-- 2a. Xoa cac truong sai khoi _HIS
DECLARE
    PROCEDURE drop_col(p_table VARCHAR2, p_col VARCHAR2) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_count FROM USER_TAB_COLS
        WHERE TABLE_NAME = p_table AND COLUMN_NAME = p_col;
        IF v_count > 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE ' || p_table || ' DROP COLUMN ' || p_col;
        END IF;
    END;
BEGIN
    drop_col('BIZ_GIAM_SAT_SOC_HIS', 'NAM_THANH_LAP');
    drop_col('BIZ_GIAM_SAT_SOC_HIS', 'SO_NHAN_SU');
    drop_col('BIZ_GIAM_SAT_SOC_HIS', 'CONG_CU_SU_DUNG');
    drop_col('BIZ_GIAM_SAT_SOC_HIS', 'SO_CANH_BAO_THANG');
END;
/

-- 2b. Them cac truong dung vao _HIS
DECLARE
    PROCEDURE add_col(p_ddl VARCHAR2, p_col VARCHAR2) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_count FROM USER_TAB_COLS
        WHERE TABLE_NAME = 'BIZ_GIAM_SAT_SOC_HIS' AND COLUMN_NAME = p_col;
        IF v_count = 0 THEN
            EXECUTE IMMEDIATE p_ddl;
        END IF;
    END;
BEGIN
    add_col('ALTER TABLE BIZ_GIAM_SAT_SOC_HIS ADD (TONG_SO_DOI_TUONG NUMBER(10) DEFAULT 0 NOT NULL)', 'TONG_SO_DOI_TUONG');
    add_col('ALTER TABLE BIZ_GIAM_SAT_SOC_HIS ADD (SO_GIAM_SAT_MOT_PHAN NUMBER(10) DEFAULT 0 NOT NULL)', 'SO_GIAM_SAT_MOT_PHAN');
    add_col('ALTER TABLE BIZ_GIAM_SAT_SOC_HIS ADD (SO_GIAM_SAT_CO_BAN NUMBER(10) DEFAULT 0 NOT NULL)', 'SO_GIAM_SAT_CO_BAN');
    add_col('ALTER TABLE BIZ_GIAM_SAT_SOC_HIS ADD (SO_GIAM_SAT_DAY_DU NUMBER(10) DEFAULT 0 NOT NULL)', 'SO_GIAM_SAT_DAY_DU');
    add_col('ALTER TABLE BIZ_GIAM_SAT_SOC_HIS ADD (SO_SU_CO NUMBER(10) DEFAULT 0 NOT NULL)', 'SO_SU_CO');
    add_col('ALTER TABLE BIZ_GIAM_SAT_SOC_HIS ADD (SO_SU_CO_DA_KHAC_PHUC NUMBER(10) DEFAULT 0 NOT NULL)', 'SO_SU_CO_DA_KHAC_PHUC');
    add_col('ALTER TABLE BIZ_GIAM_SAT_SOC_HIS ADD (LUC_LUONG_UNG_CUU NVARCHAR2(500) NULL)', 'LUC_LUONG_UNG_CUU');
END;
/
