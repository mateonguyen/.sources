-- Truong "Tinh trang" trong form Thiet bi CNTT la tuy chon (khong bat buoc),
-- nhung cot TINH_TRANG duoc tao NOT NULL tu V10 -> ORA-01400 khi de trong.
ALTER TABLE BIZ_THIET_BI_CNTT MODIFY (TINH_TRANG NULL);
