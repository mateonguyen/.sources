-- Gan CAP_DON_VI cho toan bo REF_DON_VI dang co, dua tren do sau cay
-- qua PARENT_ID (nguon du lieu chinh xac tuyet doi, khong phu thuoc quy
-- uoc dat ma/ten): LEVEL 1 (goc, PARENT_ID NULL) = CAP_0, LEVEL 2 = CAP_1,
-- LEVEL 3 = CAP_2. Don vi sau hon 3 tang (neu co, ngoai mo hinh 3 cap)
-- se KHONG duoc gan tu dong, giu nguyen CAP_DON_VI de ra soat thu cong.
MERGE INTO REF_DON_VI d
USING (
    SELECT ID, LEVEL AS LVL
    FROM REF_DON_VI
    START WITH PARENT_ID IS NULL
    CONNECT BY PRIOR ID = PARENT_ID
) lv
ON (d.ID = lv.ID)
WHEN MATCHED THEN UPDATE SET
    d.CAP_DON_VI = CASE lv.LVL
        WHEN 1 THEN N'CAP_0'
        WHEN 2 THEN N'CAP_1'
        WHEN 3 THEN N'CAP_2'
        ELSE d.CAP_DON_VI
    END
WHERE lv.LVL IN (1, 2, 3);

-- Ra soat: liet ke cac don vi ngoai mo hinh 3 cap (level > 3) de xu ly tay.
-- SELECT d.ID, d.MA_DON_VI, d.TEN_DON_VI, lv.LVL
-- FROM REF_DON_VI d
-- JOIN (
--     SELECT ID, LEVEL AS LVL
--     FROM REF_DON_VI
--     START WITH PARENT_ID IS NULL
--     CONNECT BY PRIOR ID = PARENT_ID
-- ) lv ON lv.ID = d.ID
-- WHERE lv.LVL > 3;
