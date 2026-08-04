# Ket qua test E2E luong bao cao

## Retest sau fix

### 1) Flyway migration

- Da chay `repair` va `migrate` thanh cong den version `75`.
- Migration `V75__enforce_unique_normalized_username.sql` da duoc sua de:
  - Don duplicate user theo `USER_NAME` (xoa orphan khong co role).
  - Chuan hoa `NORMALIZED_USER_NAME` an toan voi unique constraint da ton tai.
  - Sua join don vi sang `REF_DON_VI` (thay vi `DON_VI`).

### 2) Xac minh du lieu DB sau V75

- Kiem tra duplicate `NORMALIZED_USER_NAME`: `no rows selected`.
- Kiem tra user `capxa.e2e`: con 1 ban ghi hop le:
  - `ID=42`
  - `USER_NAME=capxa.e2e`
  - `NORMALIZED_USER_NAME=CAPXA.E2E`

### 3) Regression test phan quyen

- Da chay test filter:
  - `dotnet test tests/Api.IntegrationTests/ThucLuc.Api.IntegrationTests.csproj --filter "FullyQualifiedName~AuthorizationIntegrationTests"`
- Ket qua:
  - `Total: 2`
  - `Passed: 2`
  - `Failed: 0`

### 4) Cac muc con cho retest runtime thu cong

- Chua the thuc hien trong phien nay vi API `http://localhost:5283` chua duoc start:
  - Bug 1: `PATCH /api/v1/ky-bao-cao/223/status`, `POST /api/v1/snapshot/submit-current`, `POST /api/v1/yeu-cau-bo-sung`.
  - Bug 2: tao ky bao cao co ngay tu UI/API (xac nhan khong con ORA-01861).
  - Bug 3: duplicate username tra `409 USER_DUPLICATE` va role-cap mismatch khong tao orphan user.
  - Bug 4: CAP_XA vao trang home khong bi 403 va luong du lieu dung endpoint phu hop.

## Retest sau fix vong 2 (MinIO/PDF + Submit atomic + MoLai)

### 1) Ket qua sua Bug 1 (MinIO/PDF 500)

- Da thay implementation luu file sang MinIO .NET SDK (`IMinioClient`) thay cho AWS S3 SDK path cu.
- Smoke test upload file:
  - `POST /api/v1/files/upload` => `200`
  - Response mau: `success=true`, tra metadata file (`id`, `fileName`, `mimeType`, `fileSize`).
- Kiem tra PDF:
  - `GET /api/v1/snapshot/{id}/pdf` => `200`
  - `downloadUrl` duoc tra ve hop le (`PDF_URL_PRESENT=True`).

### 2) Ket qua submit-current va submit (Bug 2)

- `POST /api/v1/snapshot/submit-current` (ky=6001, donvi=2002) => `200`, tao snapshot `id=28`.
- `POST /api/v1/snapshot/29/submit` => `200`.
- `POST /api/v1/snapshot/30/submit` => `200`.
- Da xac nhan hanh vi khi da co active snapshot:
  - `POST /api/v1/snapshot/submit-current` => `422`
  - Error code: `SNAPSHOT_ALREADY_SUBMITTED`.

Ghi chu quan trong:

- Da fix them logic tinh version trong `SubmitCurrentAsync/CreateDraftAsync/MoLaiAsync` theo `MAX(PHIEN_BAN)+1` tren toan bo ban ghi (bao gom soft-deleted) de tranh ORA-00001 do unique `(KY_BAO_CAO_ID, DON_VI_ID, PHIEN_BAN)`.

### 3) Ket qua MoLai (Bug 3)

- `POST /api/v1/snapshot/mo-lai?kyBaoCaoId=6001&donViId=2002` => `200`.
- Tao snapshot moi `id=30`, `phienBan=7` (dung quy tac `MAX+1`).
- Cac snapshot active cu da duoc supersede:
  - Oracle query cho thay ver `6` chuyen `TRANG_THAI=4` (Superseded),
  - snapshot moi ver `7` vao `TRANG_THAI=3` (Locked) sau khi submit.

### 4) Kiem tra yeu-cau-bo-sung va trang thai hoan thanh

- `POST /api/v1/yeu-cau-bo-sung` => `200` (tao `id=2`).
- `POST /api/v1/yeu-cau-bo-sung/2/duyet` => `200`.
- Sau khi don vi submit lai snapshot, Oracle query xac nhan yeu cau `id=2` co `TRANG_THAI=5`, `COMPLETED_AT` da duoc set (HoanThanh).

### 5) Bang chung Oracle (trich query runtime)

- `RPT_BAO_CAO_SNAPSHOT` (ky=6001, donvi=2002):
  - ver 1 -> status 1 (Draft)
  - ver 2 -> status 4 (Superseded)
  - ver 3 -> status 4 (Superseded)
  - ver 4 -> status 4 (Superseded, da soft-delete)
  - ver 5 -> status 4 (Superseded)
  - ver 6 -> status 4 (Superseded)
  - ver 7 -> status 3 (Locked)
- `RPT_YEU_CAU_BO_SUNG` (ky=6001, donvi=2002):
  - id 1 -> status 5 (HoanThanh)
  - id 2 -> status 5 (HoanThanh)

### 6) Luu y khi retest

- `DELETE /api/v1/snapshot/23` tra `404` trong lan retest nay vi ban ghi da bi soft-delete tu truoc.
- He thong van dat cac tieu chi fix vong 2: khong con 500 do MinIO/PDF, mo-lai versioning dung, submit-current conflict tra 422 dung ma loi.

## Retest sau fix vong 3 (2 viec ton cuoi)

### 1) Bug FE CAP_XA (home/permission/guard/sidebar quick-access)

- Da xac nhan code FE da duoc gate theo quyen truoc khi goi API dashboard:
  - Home chi goi danh sach/ky hien tai khi co `ky_bao_cao:read`.
  - Home chi goi `my-tien-do` khi co `tong_hop_tien_do:xac_nhan` va da co `KyCode`.
  - Quick access duoc filter theo permission (khong render link khong duoc phep).
- Da bo sung guard cho route `/forbidden`: user da dang nhap se duoc chuyen ve `/trang-chu`.
- Sau dang nhap, FE dieu huong truc tiep ve `/trang-chu` (khong di qua route goc de tranh roi vao forbidden flow).
- Build FE thanh cong sau thay doi:
  - `npm run build` => PASS.

### 2) Bug Upload thieu field tra 400 (khong 500)

- Da bo sung validator cho `UploadFileRequest` voi cac truong bat buoc:
  - `DonViId > 0`
  - `EntityType` khong rong
  - `EntityId > 0`
  - `KyCode` khong rong
- Da bo sung validate service-level cho truong file:
  - `file` null/empty => nem `ValidationException` (field `file`).
- Build backend thanh cong sau thay doi:
  - `dotnet build backend/src/Api/ThucLuc.Api.csproj` => PASS.
- Da them integration test xac nhan hanh vi API upload:
  - File test: `backend/tests/Api.IntegrationTests/FileUploadValidationIntegrationTests.cs`
  - Cac case thieu tung field bat buoc (`DonViId`, `EntityType`, `EntityId`, `KyCode`, `file`) deu tra `400` + `VALIDATION_ERROR`.
  - Case payload day du tra `200`.
  - Lenh chay: `dotnet test backend/tests/Api.IntegrationTests/ThucLuc.Api.IntegrationTests.csproj --filter "FullyQualifiedName~FileUploadValidationIntegrationTests"`
  - Ket qua: `Total: 6, Passed: 6, Failed: 0`.

### 3) Ket luan vong 3

- Hai viec ton cuoi sau verify vong 2 da duoc fix va retest dat.
- Toan bo ke hoach test luong bao cao hoan tat.

### 4) Retest Oracle runtime (bo sung dieu kien dong vong 3)

- Da ap dung mapping bool Oracle theo huong chung trong EF Core model:
  - toan bo property `bool`/`bool?` duoc map ve `NUMBER(1)` qua `ValueConverter` dung chung.
- Da sua luong refresh token de khong insert gia tri rong/null vao cac cot bat buoc Oracle:
  - `DeviceId`, `DeviceUserAgent`, `DeviceIpAddress` duoc normalize/fallback.
  - Device ID duoc resolve tu request/header/cookie `thuc_luc_device_id`, neu khong co thi sinh moi.
  - cookie `thuc_luc_device_id` duoc set de on dinh session theo thiet bi.

Ket qua verify runtime (10:43, profile HTTPS):

- Auth flow 3 role: `h05.user`, `donvi.user`, `h05.viewer`
  - `POST /api/v1/auth/login?rememberMe=true` => `200`
  - `POST /api/v1/auth/refresh` => `200`
  - `POST /api/v1/auth/logout` => `200`
- CAP_XA flow:
  - `POST /api/v1/auth/login?rememberMe=true` (`capxa.e2e`) => `200`
  - `GET /api/v1/tong-hop-tien-do/my-tien-do?kyBaoCaoCode=2026Q3_E2E_TEST` => `200`
  - `POST /api/v1/tong-hop-tien-do/xac-nhan` => `200`

Khong con gap cac loi runtime Oracle trong cac flow tren:

- Khong con `ORA-01400` voi `IDM_REFRESH_TOKEN_SESSIONS.DEVICE_ID`.
- Khong con `InvalidCastException` (Int32/Int16 -> Boolean) khi vao `my-tien-do` hoac logout.

## B4.5 - Dong gap G1/G3/G4 (09/07/2026)

### G1 - Data scoping theo don vi (da xu ly)

- Da bo sung helper scope don vi dung chung tai Application layer:
  - tu `currentUser.DonViId` tinh tap don vi duoc xem = don vi hien tai + toan bo don vi con (de quy theo `REF_DON_VI.PARENT_ID`).
  - `system:admin` hoac don vi goc cap quan ly (`ParentId = null`/`CapDonVi = CUC`) duoc xem toan bo.
- Da ap scope vao endpoint doc trong luong bao cao:
  - `GET /api/v1/snapshot?kyBaoCaoId=...`
  - `GET /api/v1/snapshot/{id}`
  - `GET /api/v1/snapshot/{id}/pdf` (ngoai pham vi tra `404`)
  - `GET /api/v1/ky-bao-cao/{id}/tien-do`
  - `GET /api/v1/ky-bao-cao/{id}/don-vi-trang-thai`
  - `GET /api/v1/yeu-cau-bo-sung?kyBaoCaoId=...`

Ket qua verify:

- `h05.viewer` van thay toan bo snapshot va tai PDF duoc.
- `viewer.user`/`lanhdao.tinh` (CA_DN) chi thay snapshot CA_DN + don vi con; khong doc duoc snapshot/PDF cua CA_HN.

### G3 - Chan ChuanBi -> Khoa (da xu ly)

- Da sua transition rule:
  - `ChuanBi` chi duoc -> `DangMo`.
  - `DangMo/DaDong` van duoc -> `Khoa`.
- FE man Kỳ bao cao da giu hanh vi: nut Khoa chi hien voi ky `DaDong`.

Ket qua verify:

- `PATCH /api/v1/ky-bao-cao/{id}/status` tu `ChuanBi -> Khoa` tra `422` voi ma `KY_INVALID_TRANSITION`.
- Ky `ChuanBi` xoa duoc binh thuong.
- Ky `DangMo/DaDong` van khoa duoc.

### G4 - Bo sung test coverage pham vi role lanh dao/viewer (da xu ly)

- Da bo sung integration test scope snapshot, bao gom tao user `lanhdao.tinh` qua API va kiem tra 4 case:
  - `h05.viewer` thay toan bo.
  - `lanhdao.tinh` chi thay CA_DN + con.
  - `lanhdao.tinh` truy cap snapshot/PDF CA_HN bi chan.
  - `viewer.user` co cung pham vi nhu CA_DN.
