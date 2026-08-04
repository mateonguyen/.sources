# Audit ghi chú phân quyền (2026-07-07)

## 1) Tên vai trò tiếng Việt có dấu

Đã cập nhật dữ liệu tên vai trò từ không dấu sang có dấu:

- SYSTEM_ADMIN -> Quản trị hệ thống
- QUAN_LY -> Quản lý
- CAP_TINH -> Người dùng cấp tỉnh
- CAP_XA -> Người dùng cấp xã
- VIEWER -> Người xem
- LANH_DAO -> Lãnh đạo

Phạm vi sửa:

- Seeder runtime: backend/src/Infrastructure/Persistence/Seeding/BaselineDataSeeder.cs
- Flyway migration đổi tên role cũ: backend/db/flyway/sql/V50\_\_rename_roles_quan_ly_lanh_dao.sql
- Migration fallback cho môi trường không chạy seeder: backend/db/flyway/sql/V71\_\_update_role_ten_vi.sql

Rà soát nguồn SQL được yêu cầu:

- R\_\_seed_baseline_data.sql: không seed/ghi đè IDM_ROLES.TEN_ROLE
- V46\_\_normalize_role_permissions.sql: chỉ chuẩn hóa role-permissions CAP_XA, không đổi tên role
- V50\_\_rename_roles_quan_ly_lanh_dao.sql: có đổi TEN_ROLE, đã chuyển sang có dấu

Ghi chú áp dụng dữ liệu:

- Seeder là upsert theo Id/RoleCode, restart backend sẽ cập nhật TenRole trong môi trường có chạy seeder.
- Với môi trường có production guard không chạy seeder, dùng V71 để cập nhật trực tiếp TEN_ROLE.

## 2) Rà soát và chỉnh hành vi phân quyền

### Kết luận nguyên nhân "SYSTEM_ADMIN chọn vào thấy trống"

Đúng theo dữ liệu gốc:

- SYSTEM_ADMIN chỉ có 1 quyền wildcard: system:admin
- Handler cho phép system:admin đi qua mọi check quyền

UI cũ không biểu diễn rõ wildcard nên gây hiểu nhầm là role chưa có quyền.

### Đã sửa

- UI thêm banner chỉ báo role có toàn quyền system:admin và chuyển ma trận sang chế độ chỉ đọc.
- UI khóa thao tác checkbox + nút Lưu khi role hệ thống đang có system:admin.
- UI không còn ẩn các quyền action ngoài 6 cột chuẩn; các action không map trực tiếp (upload/export/pdf/admin/xac_nhan) được đưa vào cột "Khác" để vẫn hiển thị/chỉnh được.
- Backend chặn bỏ system:admin khỏi role hệ thống (IsSystem=true) khi gọi PUT /api/v1/roles/{id}/permissions.

## 3) Kết quả kiểm tra kỹ thuật

- dotnet build ThucLuc.sln: PASS
- dotnet test tests/Api.IntegrationTests/ --filter FullyQualifiedName~AuthorizationIntegrationTests: PASS (2/2)
- dotnet test tests/Api.IntegrationTests/: FAIL 8 test (cụm snapshot/ky bao cao/dao tao hoc vien), không thuộc phạm vi sửa phân quyền trong đợt này.

## 4) Trạng thái theo yêu cầu

- Seed tên role có dấu: Đã xử lý.
- Wildcard SYSTEM_ADMIN có chỉ báo rõ ràng, không còn trạng thái "trống không rõ lý do": Đã xử lý.
- Chặn bỏ system:admin của role hệ thống từ UI và BE: Đã xử lý.
- Round-trip lưu phân quyền role nghiệp vụ: logic và API giữ nguyên, không phát hiện regression từ thay đổi hiện tại.
- Verify hiển thị UI end-to-end sau restart backend: Chưa chạy thủ công trong phiên này.
