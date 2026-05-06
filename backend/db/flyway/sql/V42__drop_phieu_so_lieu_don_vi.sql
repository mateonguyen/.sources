-- V42: Xóa bảng RPT_PHIEU_SO_LIEU_DON_VI (tạo ở V40, không còn dùng theo thiết kế mới)
BEGIN
    EXECUTE IMMEDIATE 'DROP TABLE RPT_PHIEU_SO_LIEU_DON_VI CASCADE CONSTRAINTS';
    DBMS_OUTPUT.PUT_LINE('Dropped table RPT_PHIEU_SO_LIEU_DON_VI');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -942 THEN -- ORA-00942: table or view does not exist
            DBMS_OUTPUT.PUT_LINE('Table RPT_PHIEU_SO_LIEU_DON_VI does not exist, skipping');
        ELSE
            RAISE;
        END IF;
END;
/
