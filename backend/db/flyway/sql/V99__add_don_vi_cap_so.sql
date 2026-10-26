-- Chuan hoa cap don vi thanh so nguyen (CAP_SO) thay vi chi dua vao chuoi
-- CAP_DON_VI. Ly do: CAP_DON_VI da phat sinh nhieu bien the qua thoi gian
-- (CAP_0/CAP_1/CAP_2 la vocabulary hien hanh sau V86/V87, nhung 3 dong du
-- lieu fallback cu tu truoc do van con giu TINH/CUC) khien cac cho dang so
-- sanh chuoi (KyBaoCaoService, BaoCaoBienDongService, UserService...) de bi
-- bo sot du lieu neu chi khop dung 1-2 gia tri chuoi cu the. CAP_SO la nguon
-- du lieu chinh thuc, on dinh de filter/index thay cho chuoi.
ALTER TABLE REF_DON_VI ADD CAP_SO NUMBER(1);

UPDATE REF_DON_VI SET CAP_SO = CASE CAP_DON_VI
    WHEN N'CAP_0' THEN 0
    WHEN N'CAP_1' THEN 1
    WHEN N'TINH'  THEN 1
    WHEN N'CUC'   THEN 1
    WHEN N'CAP_2' THEN 2
    ELSE NULL
END;

CREATE INDEX IX_REF_DV_CAP_SO ON REF_DON_VI (CAP_SO);
