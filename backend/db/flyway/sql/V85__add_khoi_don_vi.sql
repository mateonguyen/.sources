ALTER TABLE REF_DON_VI ADD KHOI_DON_VI NVARCHAR2(20) NULL;

CREATE INDEX IX_REF_DV_KHOI ON REF_DON_VI (KHOI_DON_VI);

COMMENT ON COLUMN REF_DON_VI.KHOI_DON_VI IS 'Khoi don vi theo ky hieu Cuc/Phong nganh CAND (V/A/B/C/H/T/D/K...) - doc lap voi CAP_DON_VI, chi ap dung o cap Cuc/Phong, co the NULL o cap Bo/Tinh/Xa.';
