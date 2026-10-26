-- Du lieu demo co pham vi cho man "Bien dong & xu huong".
-- Idempotent: co the chay lai; chi cap nhat hai ky co KY_CODE bat dau BIEN_DONG_DEMO.
SET DEFINE OFF SERVEROUTPUT ON
DECLARE
    v_mau_id       NUMBER(19);
    v_don_vi_id    NUMBER(19);
    v_user_id      NUMBER(19);
    v_ky_1_id      NUMBER(19);
    v_ky_2_id      NUMBER(19);
    v_snapshot_1   NUMBER(19);
    v_snapshot_2   NUMBER(19);
    v_snapshot_3   NUMBER(19);
    v_snapshot_4   NUMBER(19);
    v_batch_1      NUMBER(19);
    v_batch_2      NUMBER(19);
    v_batch_3      NUMBER(19);
    v_batch_4      NUMBER(19);

    PROCEDURE ensure_period(
        p_code IN NVARCHAR2,
        p_name IN NVARCHAR2,
        p_quy IN NUMBER,
        p_start IN DATE,
        p_end IN DATE,
        p_id OUT NUMBER
    ) IS
    BEGIN
        SELECT MAX(ID) INTO p_id FROM RPT_KY_BAO_CAO WHERE KY_CODE = p_code;
        IF p_id IS NULL THEN
            INSERT INTO RPT_KY_BAO_CAO
                (KY_CODE, NAM, QUY, TRANG_THAI, NGAY_BAT_DAU, NGAY_KET_THUC,
                 GHI_CHU, MAU_BAO_CAO_ID, TEN_KY, CREATED_AT, UPDATED_AT, CREATED_BY, UPDATED_BY)
            VALUES
                (p_code, 2026, p_quy, 3, p_start, p_end,
                 N'Du lieu demo cho man Bien dong & xu huong', v_mau_id, p_name,
                 SYSTIMESTAMP, SYSTIMESTAMP, v_user_id, v_user_id)
            RETURNING ID INTO p_id;
        END IF;
    END;

    PROCEDURE ensure_snapshot(
        p_ky_id IN NUMBER,
        p_time IN TIMESTAMP,
        p_snapshot_id OUT NUMBER,
        p_batch_id OUT NUMBER
    ) IS
    BEGIN
        SELECT MAX(ID) INTO p_snapshot_id
          FROM RPT_BAO_CAO_SNAPSHOT
         WHERE KY_BAO_CAO_ID = p_ky_id AND DON_VI_ID = v_don_vi_id AND DELETED_AT IS NULL;

        IF p_snapshot_id IS NULL THEN
            INSERT INTO RPT_BAO_CAO_SNAPSHOT
                (KY_BAO_CAO_ID, DON_VI_ID, TRANG_THAI, PHIEN_BAN,
                 SUBMITTED_AT, SUBMITTED_BY, LOCKED_AT, LOCKED_BY, GHI_CHU,
                 CREATED_AT, UPDATED_AT, CREATED_BY, UPDATED_BY)
            VALUES
                (p_ky_id, v_don_vi_id, 3, 1,
                 p_time, v_user_id, p_time, v_user_id, N'Snapshot demo bien dong',
                 p_time, p_time, v_user_id, v_user_id)
            RETURNING ID INTO p_snapshot_id;
        END IF;

        SELECT MAX(ID) INTO p_batch_id
          FROM RPT_SNAPSHOT_BATCH
         WHERE KY_BAO_CAO_ID = p_ky_id AND DON_VI_ID = v_don_vi_id AND STATUS = 'SUCCEEDED';

        IF p_batch_id IS NULL THEN
            INSERT INTO RPT_SNAPSHOT_BATCH
                (KY_BAO_CAO_ID, DON_VI_ID, STATUS, STARTED_AT, FINISHED_AT,
                 TOTAL_ROWS, CREATED_AT, UPDATED_AT, CREATED_BY, UPDATED_BY)
            VALUES
                (p_ky_id, v_don_vi_id, 'SUCCEEDED', p_time, p_time,
                 0, p_time, p_time, v_user_id, v_user_id)
            RETURNING ID INTO p_batch_id;
        END IF;
    END;

    PROCEDURE add_metric(
        p_snapshot_id IN NUMBER,
        p_batch_id IN NUMBER,
        p_ky_id IN NUMBER,
        p_module IN NVARCHAR2,
        p_metric IN NVARCHAR2,
        p_value IN NUMBER,
        p_numerator IN NUMBER DEFAULT NULL,
        p_denominator IN NUMBER DEFAULT NULL
    ) IS
    BEGIN
        INSERT INTO RPT_SNAPSHOT_METRIC
            (SNAPSHOT_ID, SNAPSHOT_BATCH_ID, KY_BAO_CAO_ID,
             REPORT_DON_VI_ID, SOURCE_DON_VI_ID, MODULE_CODE, METRIC_CODE,
             VALUE, NUMERATOR, DENOMINATOR, METRIC_VERSION,
             CREATED_AT, UPDATED_AT, CREATED_BY, UPDATED_BY)
        VALUES
            (p_snapshot_id, p_batch_id, p_ky_id,
             v_don_vi_id, v_don_vi_id, p_module, p_metric,
             p_value, p_numerator, p_denominator, 1,
             SYSTIMESTAMP, SYSTIMESTAMP, v_user_id, v_user_id);
    END;
BEGIN
    SELECT ID INTO v_mau_id FROM RPT_MAU_BAO_CAO WHERE MA_MAU = 'NHAN_LUC';
    SELECT ID INTO v_don_vi_id FROM REF_DON_VI WHERE MA_DON_VI = 'G01.705.000';
    SELECT ID INTO v_user_id FROM IDM_USERS WHERE USER_NAME = 'h05.user';

    ensure_period('2026Q1_BIEN_DONG_DEMO', N'[TEST] Nhan luc CNTT - Quy I/2026', 1,
                  DATE '2026-01-01', DATE '2026-03-31', v_ky_1_id);
    ensure_period('2026Q2_BIEN_DONG_DEMO', N'[TEST] Nhan luc CNTT - Quy II/2026', 2,
                  DATE '2026-04-01', DATE '2026-06-30', v_ky_2_id);

    ensure_snapshot(v_ky_1_id, TIMESTAMP '2026-03-31 17:00:00', v_snapshot_1, v_batch_1);
    ensure_snapshot(v_ky_2_id, TIMESTAMP '2026-06-30 17:00:00', v_snapshot_2, v_batch_2);

    DELETE FROM RPT_SNAPSHOT_METRIC WHERE SNAPSHOT_ID IN (v_snapshot_1, v_snapshot_2);

    -- Quy I: 5 nhan su; 3/5 dat nang luc so.
    add_metric(v_snapshot_1, v_batch_1, v_ky_1_id, 'NHAN_LUC_CNTT', 'TONG_NHAN_SU', 5);
    add_metric(v_snapshot_1, v_batch_1, v_ky_1_id, 'NANG_LUC_SO', 'SO_DUOC_DANH_GIA', 5);
    add_metric(v_snapshot_1, v_batch_1, v_ky_1_id, 'NANG_LUC_SO', 'SO_DAT', 3);
    add_metric(v_snapshot_1, v_batch_1, v_ky_1_id, 'NANG_LUC_SO', 'SO_CHUA_DAT', 2);
    add_metric(v_snapshot_1, v_batch_1, v_ky_1_id, 'NANG_LUC_SO', 'TY_LE_DAT', 60, 3, 5);

    -- Quy II: nhan su giam con 3, nhung 3/3 dat nang luc so.
    add_metric(v_snapshot_2, v_batch_2, v_ky_2_id, 'NHAN_LUC_CNTT', 'TONG_NHAN_SU', 3);
    add_metric(v_snapshot_2, v_batch_2, v_ky_2_id, 'NANG_LUC_SO', 'SO_DUOC_DANH_GIA', 3);
    add_metric(v_snapshot_2, v_batch_2, v_ky_2_id, 'NANG_LUC_SO', 'SO_DAT', 3);
    add_metric(v_snapshot_2, v_batch_2, v_ky_2_id, 'NANG_LUC_SO', 'SO_CHUA_DAT', 0);
    add_metric(v_snapshot_2, v_batch_2, v_ky_2_id, 'NANG_LUC_SO', 'TY_LE_DAT', 100, 3, 3);

    -- Don vi cap tinh: co cung hai ky de kiem tra che do chi xem don vi cua minh.
    SELECT ID INTO v_don_vi_id FROM REF_DON_VI WHERE MA_DON_VI = 'G01.801.000';
    SELECT ID INTO v_user_id FROM IDM_USERS WHERE USER_NAME = 'donvi.user';

    ensure_snapshot(v_ky_1_id, TIMESTAMP '2026-03-31 17:05:00', v_snapshot_3, v_batch_3);
    ensure_snapshot(v_ky_2_id, TIMESTAMP '2026-06-30 17:05:00', v_snapshot_4, v_batch_4);

    DELETE FROM RPT_SNAPSHOT_METRIC WHERE SNAPSHOT_ID IN (v_snapshot_3, v_snapshot_4);

    -- Quy I: 7 nhan su; 4/7 dat nang luc so.
    add_metric(v_snapshot_3, v_batch_3, v_ky_1_id, 'NHAN_LUC_CNTT', 'TONG_NHAN_SU', 7);
    add_metric(v_snapshot_3, v_batch_3, v_ky_1_id, 'NANG_LUC_SO', 'SO_DUOC_DANH_GIA', 7);
    add_metric(v_snapshot_3, v_batch_3, v_ky_1_id, 'NANG_LUC_SO', 'SO_DAT', 4);
    add_metric(v_snapshot_3, v_batch_3, v_ky_1_id, 'NANG_LUC_SO', 'SO_CHUA_DAT', 3);
    add_metric(v_snapshot_3, v_batch_3, v_ky_1_id, 'NANG_LUC_SO', 'TY_LE_DAT', 57.14, 4, 7);

    -- Quy II: 8 nhan su; 6/8 dat nang luc so.
    add_metric(v_snapshot_4, v_batch_4, v_ky_2_id, 'NHAN_LUC_CNTT', 'TONG_NHAN_SU', 8);
    add_metric(v_snapshot_4, v_batch_4, v_ky_2_id, 'NANG_LUC_SO', 'SO_DUOC_DANH_GIA', 8);
    add_metric(v_snapshot_4, v_batch_4, v_ky_2_id, 'NANG_LUC_SO', 'SO_DAT', 6);
    add_metric(v_snapshot_4, v_batch_4, v_ky_2_id, 'NANG_LUC_SO', 'SO_CHUA_DAT', 2);
    add_metric(v_snapshot_4, v_batch_4, v_ky_2_id, 'NANG_LUC_SO', 'TY_LE_DAT', 75, 6, 8);

    COMMIT;
    DBMS_OUTPUT.PUT_LINE('Created demo periods ' || v_ky_1_id || ', ' || v_ky_2_id ||
                         '; snapshots ' || v_snapshot_1 || ', ' || v_snapshot_2 || ', ' ||
                         v_snapshot_3 || ', ' || v_snapshot_4 || '.');
END;
/

SET HEADING ON FEEDBACK ON PAGESIZE 100 LINESIZE 220
SELECT k.ID, k.KY_CODE, d.MA_DON_VI, d.TEN_DON_VI, s.ID AS SNAPSHOT_ID, COUNT(m.ID) AS METRIC_COUNT
  FROM RPT_KY_BAO_CAO k
  JOIN RPT_BAO_CAO_SNAPSHOT s ON s.KY_BAO_CAO_ID = k.ID
  JOIN RPT_SNAPSHOT_METRIC m ON m.SNAPSHOT_ID = s.ID
  JOIN REF_DON_VI d ON d.ID = s.DON_VI_ID
 WHERE k.KY_CODE IN ('2026Q1_BIEN_DONG_DEMO', '2026Q2_BIEN_DONG_DEMO')
 GROUP BY k.ID, k.KY_CODE, d.MA_DON_VI, d.TEN_DON_VI, s.ID
 ORDER BY k.KY_CODE, d.MA_DON_VI;
EXIT
