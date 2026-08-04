-- Dev-only cleanup script after retest v2.
-- Keep snapshot 23 as active (Locked), supersede old locked snapshot(s).

UPDATE RPT_BAO_CAO_SNAPSHOT
SET TRANG_THAI = 4,
    UPDATED_AT = SYSTIMESTAMP,
    UPDATED_BY = 5001
WHERE ID IN (21)
  AND KY_BAO_CAO_ID = 6001
  AND DON_VI_ID = 2002
  AND TRANG_THAI IN (2, 3);

-- Optional: mark orphan draft as superseded if you want a clean history timeline.
-- UPDATE RPT_BAO_CAO_SNAPSHOT
-- SET TRANG_THAI = 4,
--     UPDATED_AT = SYSTIMESTAMP,
--     UPDATED_BY = 5001
-- WHERE ID = 1
--   AND KY_BAO_CAO_ID = 6001
--   AND DON_VI_ID = 2002
--   AND TRANG_THAI = 1;

COMMIT;
