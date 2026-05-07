-- V64: Remove LOAI_DV_THONG_KE from BIZ_HA_TANG_MANG + _HIS
--       Remove LOAI_PHAN_MEM from BIZ_HE_THONG_THONG_TIN + _HIS
--
-- Ly do:
--   BIZ_HA_TANG_MANG    : moi don vi chi thuoc 1 loai DV thong ke;
--                         cot LOAI_DV_THONG_KE (key phan biet 2 dong/don vi) khong con can thiet.
--   BIZ_HE_THONG_THONG_TIN: bo phan loai phan mem theo loai (DUNG_CHUNG / TU_PHAT_TRIEN).

-- ═══════════════════════════════════════════════════════════════
-- PHAN 1 — BIZ_HA_TANG_MANG
-- ═══════════════════════════════════════════════════════════════

-- 1a. Drop unique index UX_BIZ_HTM_DV_LOAI (DON_VI_ID, LOAI_DV_THONG_KE)
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM USER_INDEXES WHERE INDEX_NAME = 'UX_BIZ_HTM_DV_LOAI';
    IF v_count > 0 THEN
        EXECUTE IMMEDIATE 'DROP INDEX UX_BIZ_HTM_DV_LOAI';
    END IF;
END;
/

-- 1b. Drop regular index IX_BIZ_HTM_DV (DON_VI_ID) — se tao lai sau khi dedup
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM USER_INDEXES WHERE INDEX_NAME = 'IX_BIZ_HTM_DV';
    IF v_count > 0 THEN
        EXECUTE IMMEDIATE 'DROP INDEX IX_BIZ_HTM_DV';
    END IF;
END;
/

-- 1c. Dedup: moi don vi chi giu 1 dong (MAX ID) trong so cac dong chua xoa
--     Neu co 2 dong (1 per loai), giu dong co ID lon nhat
DELETE FROM BIZ_HA_TANG_MANG
WHERE DELETED_AT IS NULL
  AND ID NOT IN (
      SELECT MAX(ID)
      FROM BIZ_HA_TANG_MANG
      WHERE DELETED_AT IS NULL
      GROUP BY DON_VI_ID
  );

-- 1d. Drop cot LOAI_DV_THONG_KE khoi bang live
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count
    FROM USER_TAB_COLS
    WHERE TABLE_NAME = 'BIZ_HA_TANG_MANG' AND COLUMN_NAME = 'LOAI_DV_THONG_KE';
    IF v_count > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE BIZ_HA_TANG_MANG DROP COLUMN LOAI_DV_THONG_KE';
    END IF;
END;
/

-- 1e. Tao unique index moi tren DON_VI_ID (chi ap dung cho dong chua xoa)
--     CASE ... ELSE NULL: NULL khong duoc index trong unique index Oracle
--     => dong da xoa (DELETED_AT IS NOT NULL) bi loai khoi rang buoc
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM USER_INDEXES WHERE INDEX_NAME = 'UX_BIZ_HTM_DV';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE UNIQUE INDEX UX_BIZ_HTM_DV ON BIZ_HA_TANG_MANG (
                CASE WHEN DELETED_AT IS NULL THEN DON_VI_ID ELSE NULL END
            )';
    END IF;
END;
/

-- ═══════════════════════════════════════════════════════════════
-- PHAN 2 — BIZ_HA_TANG_MANG_HIS
-- ═══════════════════════════════════════════════════════════════

-- 2a. Drop cot LOAI_DV_THONG_KE khoi bang _HIS
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count
    FROM USER_TAB_COLS
    WHERE TABLE_NAME = 'BIZ_HA_TANG_MANG_HIS' AND COLUMN_NAME = 'LOAI_DV_THONG_KE';
    IF v_count > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE BIZ_HA_TANG_MANG_HIS DROP COLUMN LOAI_DV_THONG_KE';
    END IF;
END;
/

-- ═══════════════════════════════════════════════════════════════
-- PHAN 3 — BIZ_HE_THONG_THONG_TIN
-- ═══════════════════════════════════════════════════════════════

-- 3a. Drop cot LOAI_PHAN_MEM khoi bang live
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count
    FROM USER_TAB_COLS
    WHERE TABLE_NAME = 'BIZ_HE_THONG_THONG_TIN' AND COLUMN_NAME = 'LOAI_PHAN_MEM';
    IF v_count > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE BIZ_HE_THONG_THONG_TIN DROP COLUMN LOAI_PHAN_MEM';
    END IF;
END;
/

-- ═══════════════════════════════════════════════════════════════
-- PHAN 4 — BIZ_HE_THONG_THONG_TIN_HIS
-- ═══════════════════════════════════════════════════════════════

-- 4a. Drop cot LOAI_PHAN_MEM khoi bang _HIS
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count
    FROM USER_TAB_COLS
    WHERE TABLE_NAME = 'BIZ_HE_THONG_THONG_TIN_HIS' AND COLUMN_NAME = 'LOAI_PHAN_MEM';
    IF v_count > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE BIZ_HE_THONG_THONG_TIN_HIS DROP COLUMN LOAI_PHAN_MEM';
    END IF;
END;
/
