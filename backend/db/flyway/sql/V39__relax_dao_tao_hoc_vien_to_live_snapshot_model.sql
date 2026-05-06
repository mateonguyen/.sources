DECLARE
    v_index_count NUMBER := 0;
BEGIN
    BEGIN
        EXECUTE IMMEDIATE 'ALTER TABLE BIZ_DAO_TAO_HOC_VIEN MODIFY (KY_BAO_CAO_CODE NULL)';
    EXCEPTION
        WHEN OTHERS THEN NULL;
    END;

    UPDATE BIZ_DAO_TAO_HOC_VIEN target
       SET target.DELETED_AT = NVL(target.DELETED_AT, SYSTIMESTAMP)
     WHERE target.ID IN (
        SELECT id
          FROM (
            SELECT ID,
                   DELETED_AT,
                   ROW_NUMBER() OVER (
                       PARTITION BY DON_VI_ID, UPPER(NOI_DUNG_DAO_TAO)
                       ORDER BY CASE WHEN DELETED_AT IS NULL THEN 0 ELSE 1 END,
                                NVL(UPDATED_AT, CREATED_AT) DESC,
                                ID DESC
                   ) AS rn
              FROM BIZ_DAO_TAO_HOC_VIEN
          ) ranked
         WHERE ranked.rn > 1
           AND ranked.DELETED_AT IS NULL
     );

    BEGIN
        EXECUTE IMMEDIATE 'DROP INDEX UX_BIZ_DTHV_DV_KY_ND';
    EXCEPTION
        WHEN OTHERS THEN NULL;
    END;

    BEGIN
        EXECUTE IMMEDIATE 'DROP INDEX UX_BIZ_DTHV_DV_ND';
    EXCEPTION
        WHEN OTHERS THEN NULL;
    END;

    BEGIN
        EXECUTE IMMEDIATE 'DROP INDEX UX_BIZ_DTHV_DV_ND_ACTIVE';
    EXCEPTION
        WHEN OTHERS THEN NULL;
    END;

    SELECT COUNT(*)
      INTO v_index_count
      FROM USER_INDEXES
     WHERE INDEX_NAME = 'UX_BIZ_DTHV_DV_ND_ACTIVE';

    IF v_index_count = 0 THEN
        EXECUTE IMMEDIATE q'[
            CREATE UNIQUE INDEX UX_BIZ_DTHV_DV_ND_ACTIVE
                ON BIZ_DAO_TAO_HOC_VIEN (
                    CASE WHEN DELETED_AT IS NULL THEN DON_VI_ID END,
                    CASE WHEN DELETED_AT IS NULL THEN UPPER(NOI_DUNG_DAO_TAO) END
                )
        ]';
    END IF;
END;
/