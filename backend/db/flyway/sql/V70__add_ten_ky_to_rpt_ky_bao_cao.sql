-- Thêm cột TEN_KY (tên kỳ báo cáo do người dùng đặt, unique, nullable để tương thích row cũ)
ALTER TABLE RPT_KY_BAO_CAO ADD TEN_KY NVARCHAR2(200);

-- Oracle unique index tự loại NULL, nên các row cũ (TEN_KY = NULL) không xung đột
CREATE UNIQUE INDEX IX_RPT_KY_BAO_CAO_TEN_KY ON RPT_KY_BAO_CAO (TEN_KY);
