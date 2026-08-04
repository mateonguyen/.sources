# Kết quả kiểm chứng dữ liệu phân quyền (2026-07-07)

## Phạm vi và nguồn đối chiếu

- Ma trận chuẩn: theo yêu cầu kiểm chứng trong prompt hiện tại (không tìm thấy file Docs/ke-hoach-test-luong-bao-cao.md trong workspace).
- Seed code: `SeedRolePermissionsAsync()` trong `BaselineDataSeeder`.
- Dữ liệu DB đang chạy: query join `IDM_ROLES` + `IDM_ROLE_PERMISSIONS` + `IDM_PERMISSIONS`.
- Runtime: `POST /api/v1/auth/login` + `GET /api/v1/auth/me` cho các user seed.
- Smoke authorization: 4 case 403 theo yêu cầu.

## 1) Đối chiếu seed code vs ma trận chuẩn

### Kết luận nhanh

- Có lệch trong seed code.
- `SYSTEM_ADMIN` đúng mô hình wildcard (chỉ `system:admin`).

### Các ô lệch (chỉ liệt kê lệch)

| Nhóm quyền                        | Kỳ vọng | Thực tế seed | Trạng thái   |
| --------------------------------- | ------- | ------------ | ------------ |
| `ky_bao_cao:update` cho `QUAN_LY` | Có      | Không có     | LỆCH (thiếu) |

### Bằng chứng code

- `QUAN_LY` có `ky_bao_cao:create`, `ky_bao_cao:approve`, nhưng không có `ky_bao_cao:update` trong mapping `SeedRolePermissionsAsync()`.
- `SYSTEM_ADMIN` mapping chỉ có `Permissions.SystemAdmin`.

## 2) Đối chiếu DB đang chạy vs seed code và ma trận chuẩn

### Kết quả query DB (tóm tắt số quyền theo role)

- `SYSTEM_ADMIN`: 1
- `QUAN_LY`: 105
- `CAP_TINH`: 91
- `CAP_XA`: 62
- `LANH_DAO`: 10
- `VIEWER`: 9

### Lệch so với ma trận chuẩn

| Nhóm quyền                                         | Role      | Kỳ vọng         | Thực tế DB             | Trạng thái   |
| -------------------------------------------------- | --------- | --------------- | ---------------------- | ------------ |
| `ky_bao_cao:update`                                | `QUAN_LY` | Có              | Không có               | LỆCH (thiếu) |
| `du_an_cntt:delete` (nhóm module nghiệp vụ delete) | `CAP_XA`  | Không có delete | Có `du_an_cntt:delete` | LỆCH (thừa)  |

### Nhận định về migration ảnh hưởng lệch

- `V46__normalize_role_permissions.sql`:
  - Chỉ xóa `snapshot:create`, `snapshot:update`, `snapshot:submit` khỏi `CAP_XA`.
  - Không gây lệch với ma trận chuẩn (ma trận cũng kỳ vọng CAP_XA không có các quyền này).
- `V69__grant_du_an_cntt_permissions.sql`:
  - Insert `du_an_cntt:read/create/update/delete` cho `QUAN_LY`, `CAP_TINH`, `CAP_XA`.
  - Đây là nguyên nhân trực tiếp làm `CAP_XA` bị thừa `du_an_cntt:delete` so với ma trận chuẩn.

### Ghi chú thêm (không thuộc ma trận chuẩn đang kiểm)

- DB hiện có thêm nhóm quyền `dao_tao_nuoc_ngoai:*` cho role nghiệp vụ. Đây là mở rộng ngoài ma trận kỳ vọng trong prompt hiện tại.

## 3) Đối chiếu runtime (`/auth/me`) theo user seed

### Kết quả runtime

- `h05.user` (QUAN_LY): `perm_count=105`
- `donvi.user` (CAP_TINH): `perm_count=91`
- `viewer.user` (VIEWER): `perm_count=9`
- `h05.viewer` (LANH_DAO): `perm_count=10`

### Kết luận runtime

- Runtime khớp với dữ liệu role-permission trong DB hiện tại.
- Vì DB đang lệch ma trận ở 2 điểm nêu trên, runtime cũng phản ánh đúng các lệch đó.

## 4) Smoke check 403

Tất cả 4 case đều đúng kỳ vọng (403):

1. `donvi.user` -> `POST /api/v1/ky-bao-cao` -> **403**
2. `donvi.user` -> `POST /api/v1/yeu-cau-bo-sung/1/duyet` -> **403**
3. `h05.user` -> `POST /api/v1/tong-hop-tien-do/xac-nhan` -> **403**
4. `h05.viewer` -> `POST /api/v1/snapshot/create-draft` -> **403**

## 5) Tổng hợp chênh lệch cần xử lý

### Lệch cần fix

1. Thiếu `ky_bao_cao:update` cho `QUAN_LY`.
2. Thừa `du_an_cntt:delete` cho `CAP_XA`.

### Đề xuất sửa (chưa áp dụng trong phiên này)

- Seed code (`SeedRolePermissionsAsync`):
  - Thêm `Permissions.KyBaoCao.Update` vào `QUAN_LY`.
  - Gỡ `Permissions.DuAnCntt.Delete` khỏi `CAP_XA`.
- Migration DB đồng bộ dữ liệu đang chạy:
  - Thêm migration mới để:
    - `INSERT` quyền `ky_bao_cao:update` cho `QUAN_LY` nếu chưa có.
    - `DELETE` quyền `du_an_cntt:delete` khỏi `CAP_XA`.

## 6) Trạng thái nghiệm thu hiện tại

- Mọi ô trong ma trận chuẩn đã được đánh dấu ĐÚNG/LỆCH theo seed + DB + runtime + smoke.
- 4 smoke check 403: PASS.
- Còn 2 lệch dữ liệu phân quyền cần fix trước khi kết luận "sạch lệch".

---

## 7) Cập nhật sau khi áp fix (2026-07-07)

### Thay đổi đã áp dụng

- Seed:
  - Thêm `Permissions.KyBaoCao.Update` vào catalog quyền baseline.
  - Thêm `Permissions.KyBaoCao.Update` cho role `QUAN_LY`.
  - Gỡ `Permissions.DuAnCntt.Delete` khỏi role `CAP_XA`.
- Migration:
  - `V72__align_role_permissions_with_matrix.sql`: căn chỉnh role-permission theo ma trận.
  - `V73__add_ky_bao_cao_update_and_grant_quan_ly.sql`: thêm permission `ky_bao_cao:update` nếu thiếu và grant cho `QUAN_LY`.
  - `V74__remove_cap_xa_du_an_cntt_delete.sql`: đảm bảo `CAP_XA` không có `du_an_cntt:delete`.

### Kết quả verify DB sau fix

- `perm ky_bao_cao:update exists` = **1**
- `QUAN_LY has ky_bao_cao:update` = **1**
- `CAP_XA has du_an_cntt:delete` = **0**

Tổng số quyền theo role sau fix:

- `SYSTEM_ADMIN`: 1
- `QUAN_LY`: 106
- `CAP_TINH`: 91
- `CAP_XA`: 61
- `LANH_DAO`: 10
- `VIEWER`: 9

### Kết quả verify runtime (`/auth/me`) sau fix

- `h05.user`: `perm_count=106`
- `donvi.user`: `perm_count=91`
- `viewer.user`: `perm_count=9`
- `h05.viewer`: `perm_count=10`

### Smoke check 403 sau fix

1. `donvi.user` -> `POST /api/v1/ky-bao-cao` -> **403**
2. `donvi.user` -> `POST /api/v1/yeu-cau-bo-sung/1/duyet` -> **403**
3. `h05.user` -> `POST /api/v1/tong-hop-tien-do/xac-nhan` -> **403**
4. `h05.viewer` -> `POST /api/v1/snapshot/create-draft` -> **403**

### Test theo tiêu chí nghiệm thu

- Chạy: `dotnet test tests/Api.IntegrationTests/ThucLuc.Api.IntegrationTests.csproj --filter "FullyQualifiedName~AuthorizationIntegrationTests"`
- Kết quả: **PASS 2/2**, failed 0.

### Kết luận sau fix

- 2 lệch phân quyền đã được xử lý xong ở cả seed và DB runtime.
- Runtime và smoke đều phản ánh đúng ma trận kỳ vọng.
- Tiêu chí test AuthorizationIntegrationTests: PASS.
