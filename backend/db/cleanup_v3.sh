#!/bin/bash
# Clean up any partially created V3 objects and re-run
sqlplus -S 'CAND_QLCNTT/"123456"@XEPDB1' <<-SQL
-- Drop V3 objects if they were partially created
DECLARE
  PROCEDURE drop_if_exists(p_type VARCHAR2, p_name VARCHAR2) IS
  BEGIN
    EXECUTE IMMEDIATE 'DROP ' || p_type || ' ' || p_name;
  EXCEPTION
    WHEN OTHERS THEN NULL;
  END;
BEGIN
  drop_if_exists('TABLE', 'THONG_BAO');
  drop_if_exists('TABLE', 'FILE_DINH_KEM');
  drop_if_exists('TABLE', 'SYSTEM_LOG');
END;
/
EXIT;
SQL
