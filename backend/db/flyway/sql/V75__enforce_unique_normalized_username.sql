-- Ensure NORMALIZED_USER_NAME is populated and enforce uniqueness.
-- Also remove known bad duplicate account created by partial failure in legacy flow.

-- First, cleanup duplicate usernames where one row is orphan (no roles) and another is active.
-- This avoids ORA-00001 when an existing unique index/constraint already enforces NORMALIZED_USER_NAME.
DELETE FROM IDM_USERS U
WHERE EXISTS (
    SELECT 1
    FROM IDM_USERS U2
    WHERE U2.ID <> U.ID
      AND UPPER(TRIM(U2.USER_NAME)) = UPPER(TRIM(U.USER_NAME))
)
AND NOT EXISTS (
    SELECT 1
    FROM IDM_USER_ROLE_ASSIGNMENTS URA
    WHERE URA.USER_ID = U.ID
)
AND EXISTS (
    SELECT 1
    FROM IDM_USERS U3
    WHERE U3.ID <> U.ID
      AND UPPER(TRIM(U3.USER_NAME)) = UPPER(TRIM(U.USER_NAME))
      AND EXISTS (
          SELECT 1
          FROM IDM_USER_ROLE_ASSIGNMENTS URA2
          WHERE URA2.USER_ID = U3.ID
      )
);

UPDATE IDM_USERS
SET NORMALIZED_USER_NAME = UPPER(TRIM(USER_NAME))
WHERE NORMALIZED_USER_NAME IS NULL
   OR NORMALIZED_USER_NAME <> UPPER(TRIM(USER_NAME));

-- Remove known orphan duplicate: capxa.e2e user under CA_HN with no assigned role.
DELETE FROM IDM_USERS U
WHERE UPPER(TRIM(U.USER_NAME)) = 'CAPXA.E2E'
  AND EXISTS (
      SELECT 1
    FROM REF_DON_VI D
      WHERE D.ID = U.DON_VI_ID
        AND UPPER(TRIM(D.MA_DON_VI)) = 'CA_HN'
  )
  AND NOT EXISTS (
      SELECT 1
      FROM IDM_USER_ROLE_ASSIGNMENTS URA
      WHERE URA.USER_ID = U.ID
  );

-- Generic safety cleanup: for duplicated normalized usernames, delete orphan rows without roles,
-- keeping rows that already have role assignments.
DELETE FROM IDM_USERS U
WHERE EXISTS (
    SELECT 1
    FROM IDM_USERS U2
    WHERE U2.NORMALIZED_USER_NAME = U.NORMALIZED_USER_NAME
      AND U2.ID <> U.ID
)
AND NOT EXISTS (
    SELECT 1
    FROM IDM_USER_ROLE_ASSIGNMENTS URA
    WHERE URA.USER_ID = U.ID
)
AND EXISTS (
    SELECT 1
    FROM IDM_USERS U3
    WHERE U3.NORMALIZED_USER_NAME = U.NORMALIZED_USER_NAME
      AND U3.ID <> U.ID
      AND EXISTS (
          SELECT 1
          FROM IDM_USER_ROLE_ASSIGNMENTS URA2
          WHERE URA2.USER_ID = U3.ID
      )
);

DECLARE
    v_unique_idx_count NUMBER;
BEGIN
    -- Create the unique index only when no unique index currently exists on NORMALIZED_USER_NAME.
    SELECT COUNT(*)
      INTO v_unique_idx_count
      FROM USER_INDEXES UI
      JOIN USER_IND_COLUMNS UIC ON UIC.INDEX_NAME = UI.INDEX_NAME
     WHERE UI.TABLE_NAME = 'IDM_USERS'
       AND UI.UNIQUENESS = 'UNIQUE'
       AND UIC.COLUMN_NAME = 'NORMALIZED_USER_NAME';

    IF v_unique_idx_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE UNIQUE INDEX UX_IDM_USERS_NORMALIZED_USER_NAME ON IDM_USERS (NORMALIZED_USER_NAME)';
    END IF;
END;
/
