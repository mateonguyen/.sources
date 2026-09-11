# Quy trình triển khai, backup và restore cơ sở dữ liệu Oracle

> Trạng thái: Bản nháp để thống nhất phương án trước khi triển khai công cụ.
>
> Kịch bản triển khai ban đầu: DEV là nguồn và TEST là đích của hệ thống Thực Lực V2.
> Bản thân DbManager chỉ dùng khái niệm **Nguồn** và **Đích**, không khóa theo tên môi trường.

Tài liệu này thuộc phạm vi vận hành toàn hệ thống, không thuộc riêng backend,
frontend hay hạ tầng.

## Tóm tắt bàn giao cho phiên làm việc mới

Phần này là nguồn thông tin ngắn gọn để tiếp tục công việc khi mở cửa sổ chat
hoặc phiên phát triển mới.

### Quyết định đã chốt

- Công cụ vận hành là **web app nội bộ**, không làm desktop app.
- Công nghệ: **.NET 8 Blazor Server**.
- Tên và vị trí: `ops/db-manager/src/ThucLuc.DbManager`.
- Ops Console độc lập với Angular frontend và backend API nghiệp vụ.
- Người dùng thao tác bằng trình duyệt; Data Pump, Flyway và tài khoản DB chỉ nằm ở
  máy chạy Ops Console.
- Không public Ops Console ra Internet.
- Công cụ không phân loại DEV/TEST/PROD trên giao diện. Một kết nối giữ vai trò **Nguồn** để backup,
  kết nối còn lại giữ vai trò **Đích** để restore.
- Backup/restore dùng Oracle Data Pump; thay đổi cấu trúc hằng ngày dùng Flyway.
- DB và MinIO là hai vùng dữ liệu riêng, cần backup/mirror độc lập.

### Trạng thái source hiện tại

- Project build Release thành công, không có warning/error.
- Trang chính và `/health/live` đã chạy thử thành công trên localhost.
- Dashboard và chức năng **Kiểm tra kết nối** đã hoạt động.
- Kiểm tra kết nối gồm: liên lạc host/port và đăng nhập Oracle thật bằng ODP.NET.
- Kết nối nguồn/đích được thêm/sửa ngay trên màn hình **Sao lưu & phục hồi**, không còn tab riêng.
- Có luồng chọn/tạo database con (PDB) tại thẻ kết nối đích, sau đó xác minh và lưu service mới.
  Đây là bước riêng trước phục hồi, không tự tạo PDB khi bấm tạo schema.
  Xem [hướng dẫn chọn/tạo PDB](oracle-pdb-selection-and-restore.md). Chưa xác nhận end-to-end
  chức năng tạo PDB trên máy chủ Oracle thật.
- Tài khoản Oracle nhập trên giao diện được mã hóa trong local state; biến môi
  trường là cơ chế override dành cho server/CI.
- Khi thiếu quyền Data Pump, màn hình backup cho phép nhập mật khẩu SYS tạm thời để cấp
  `READ`, `WRITE` trên `DATA_PUMP_DIR`. SYS không được lưu và không được dùng để chạy backup.
- Biểu mẫu restore có ba chế độ: dùng schema trống, tạo schema mới hoặc thay thế schema đang có.
  Kết nối đích chỉ cần host/port/Service; schema và mật khẩu được nhập tại bước restore.
- Kiểm tra DB đích phân biệt CDB/PDB với non-CDB. `CDB$ROOT` bị chặn; database non-CDB được phép
  tạo/thay thế schema và không sử dụng mệnh đề `CONTAINER` trong `CREATE USER`.
- Backup Oracle đã chạy `DBMS_DATAPUMP` qua ODP.NET từ một DB nguồn, tải dump về vùng `backups`, ghi
  `export.log`, `manifest.json` và SHA-256. Mật khẩu không nằm trong command line hoặc log.
- Restore vào DB đích đã chạy thật qua `DBMS_DATAPUMP`: xác minh file, chuyển dump lên Oracle Directory,
  import vào schema trống và tải log về máy Ops Console. Chế độ thay thế bắt buộc tạo và xác minh
  bản sao an toàn của schema hiện tại trước khi xóa/tạo lại rồi import.
- Migration và clone vẫn là khung an toàn, chưa chạy thật.
- File cấu hình thực tế là `ops/db-manager/config/environments.json` và đã được
  `.gitignore`; file mẫu là `environments.example.json`.
- Username/password không nằm trong JSON mà lấy từ biến môi trường có tên được
  khai báo trong JSON.

### Việc tiếp theo

1. Nhập cấu hình nguồn/đích và kiểm tra kết nối máy chủ/PDB.
2. Chạy thử backup nguồn; xác minh dump, manifest và SHA-256.
3. Kiểm thử end-to-end cả schema trống và chế độ thay thế; đối chiếu bản sao an toàn, số bảng và dữ liệu.
4. Bổ sung job queue và nhật ký bền vững để thao tác không phụ thuộc phiên trình duyệt.
5. Bổ sung cơ chế dọn file Data Pump tạm trên Oracle sau khi import.
6. Bổ sung Flyway info/validate/migrate, rồi mới làm clone DEV → TEST và MinIO.

Restore chỉ dùng với kết nối mang vai trò Đích. Không import chồng vào schema có dữ liệu: chọn chế độ
thay thế để miniapp sao lưu, xóa và tạo lại schema. Luôn kiểm tra đúng Service/PDB, bản sao và schema đích.

### Thuật ngữ hiển thị cho người dùng

- **Kiểm tra kết nối**: app thử liên lạc host/port, sau đó đăng nhập Oracle nếu
  đã có tài khoản. Trong source, luồng này có tên kỹ thuật là `Preflight`.
- **Tài khoản DB**: tên đăng nhập và mật khẩu Oracle. Trong source hoặc tài liệu
  kỹ thuật có thể gọi là credential/secret.
- **Công cụ sao lưu**: Oracle Data Pump qua API `DBMS_DATAPUMP`.
- **Cập nhật cấu trúc**: chạy các file Flyway để thêm/sửa bảng, cột và chỉ mục.
- **Chỉ dùng trong mạng nội bộ**: Ops Console được mở qua địa chỉ mạng quản trị
  của đơn vị hoặc localhost, không mở cổng để mọi người ngoài Internet truy cập.
  Cụm “Không public Internet” đã được bỏ khỏi giao diện vì không phải một trạng
  thái thao tác của người dùng.

## Trạng thái triển khai Ops Console

Khung mini app đã được tạo tại `ops/db-manager` bằng .NET 8 Blazor Server.

Đã có:

- Dashboard nguồn/đích và giao diện luồng sao lưu–phục hồi.
- Kiểm tra host/port Oracle và đăng nhập DB thật khi đã có tài khoản.
- Kiểm tra tài khoản Oracle được nhập trên UI hoặc cung cấp bằng biến môi trường.
- Kiểm tra `expdp`, `impdp`, Docker và thư mục Flyway SQL.
- Màn hình backup/restore thật, danh sách các bản sao gần đây và nhật ký vận hành trong phiên chạy.
- Health endpoint `/health/live`.
- Data Protection key tách riêng, được DPAPI bảo vệ khi chạy trên Windows.

Đang chủ động khóa hoặc giới hạn:

- Tự động ngắt session chỉ được dùng trong chế độ thay thế đã xác nhận; vẫn phải dừng backend trước
  để tránh hủy giao dịch đang chạy hoặc tiến trình ứng dụng tự kết nối lại.
- Restore vào kết nối không mang vai trò Đích.
- Chạy Flyway từ giao diện.
- Clone DEV sang TEST và mirror MinIO.

Các thao tác thay đổi dữ liệu chỉ được mở sau khi kiểm tra kết nối, execution layer,
manifest/checksum và xác nhận an toàn được hoàn thiện.

## 1. Mục tiêu

- Dựng được cơ sở dữ liệu TEST từ dữ liệu DEV hiện tại.
- Cho phép DEV và TEST hoạt động độc lập, song song.
- Có thể backup TEST trước mỗi lần thay đổi cấu trúc dữ liệu.
- Có thể phục hồi TEST khi migration hoặc dữ liệu gặp sự cố.
- Có thể chủ động làm mới dữ liệu TEST từ DEV khi thật sự cần thiết.
- Mọi thay đổi cấu trúc DB phải truy vết được theo phiên bản source code.
- Không lưu mật khẩu DB hoặc thông tin nhạy cảm trong Git.

## 2. Nguyên tắc sử dụng

Backup/restore và Flyway phục vụ hai mục đích khác nhau, không thay thế cho nhau.

| Nhu cầu | Công cụ chính | Ghi chú |
|---|---|---|
| Dựng TEST lần đầu từ DEV | Oracle Data Pump (`expdp`/`impdp`) | Sao chép cả cấu trúc và dữ liệu |
| Thêm/sửa bảng, cột, index, constraint | Flyway | Không ghi đè dữ liệu tester đã nhập |
| Làm mới toàn bộ dữ liệu TEST từ DEV | Data Pump | Chỉ thực hiện có chủ đích |
| Backup TEST trước khi deploy | Data Pump | Tạo điểm phục hồi an toàn |
| Phục hồi sau sự cố | Data Pump | Dừng ứng dụng trước khi restore |
| Đồng bộ file đính kèm/PDF | MinIO backup/mirror | Backup DB không chứa nội dung file |

Quy tắc bắt buộc:

1. Thay đổi cấu trúc phải được tạo thành file Flyway mới, ví dụ `V90__add_xxx.sql`.
2. Không sửa nội dung migration đã chạy trên TEST, trừ khi có quy trình xử lý lỗi riêng.
3. Không restore DEV lên TEST trong mỗi lần deploy thông thường.
4. Trước khi restore hoặc migration có rủi ro, phải backup TEST.
5. Restore luôn phải yêu cầu xác nhận chính xác môi trường đích.
6. Không cho phép thao tác restore vào PROD trong giai đoạn xây dựng công cụ ban đầu.

## 3. Mô hình môi trường đề xuất

```text
DEV
├── Oracle DEV
├── MinIO DEV
├── Backend DEV
└── Frontend DEV

TEST
├── Oracle TEST
├── MinIO TEST
├── Gotenberg TEST
├── Backend TEST
└── Frontend TEST
```

DEV và TEST phải sử dụng database/schema, bucket MinIO và cấu hình ứng dụng riêng biệt.

Tên minh họa:

| Môi trường | Oracle service | Schema | MinIO bucket |
|---|---|---|---|
| DEV | `THUCLUCDEV` | `CAND_QLCNTT` | `thuc-luc-dev` |
| TEST | `THUCLUCTEST` | `CAND_QLCNTT` | `thuc-luc-test` |

Tên thực tế sẽ được điều chỉnh theo hạ tầng máy chủ.

### Vị trí các thành phần trong repository

Các thành phần nên được phân chia theo trách nhiệm như sau:

```text
docs/
└── operations/                         # Runbook vận hành toàn hệ thống
    └── database-deployment-backup-restore-runbook.md

ops/
└── db-manager/                         # Công cụ dành cho người triển khai/DB operator
    ├── src/
    │   └── ThucLuc.DbManager/          # Mini app .NET 8
    ├── scripts/                        # Script CLI dùng chung và xử lý khẩn cấp
    ├── config/
    │   └── environments.example.json   # Mẫu cấu hình, không chứa secret
    └── README.md

infra/
├── docker-compose.yml                  # MinIO, Gotenberg và các service hạ tầng
├── environments/                       # Mẫu cấu hình triển khai DEV/TEST
├── reverse-proxy/                      # Cấu hình proxy/TLS nếu có
└── README.md

backend/                                # Source API và migration thuộc ứng dụng
├── src/
└── db/flyway/sql/

frontend/                               # Source Angular và cấu hình build theo môi trường
└── src/
```

Quy ước sở hữu:

- `backend` chứa source API và file Flyway vì migration được phát triển cùng domain model.
- `frontend` chứa source giao diện và cấu hình endpoint theo build environment.
- `infra` chứa định nghĩa cách các service được dựng và kết nối trên máy chủ.
- `ops` chứa công cụ mà người vận hành dùng để backup, restore, migrate và clone môi trường.
- `docs/operations` chứa quy trình phối hợp tất cả các thành phần trên.

Mini app không nằm trong `backend` và cũng không được deploy chung vào API nghiệp vụ.

### Cách chạy ngay khi chưa cấu hình DB

Ops Console không bắt buộc phải có thông tin DB để khởi động. Có thể chạy ngay
để xem giao diện; ứng dụng không tự động kết nối, backup, migrate hoặc restore.

Từ root repository:

```powershell
cd .\ops\db-manager
dotnet run --project .\src\ThucLuc.DbManager\ThucLuc.DbManager.csproj
```

Nếu chưa có `config/environments.json`, trang Tổng quan hiển thị trạng thái chưa cấu hình.
Người dùng mở **Sao lưu & phục hồi** và nhập hai kết nối ngay trên đầu màn hình;
không có tab cấu hình riêng và ứng dụng không tự kết nối DB khi vừa khởi động.

Kiểm tra kết nối thất bại không làm thay đổi DB và không ngăn người dùng xem các màn
hình còn lại.

### Cấu hình để bắt đầu kiểm tra kết nối thật

Luồng sử dụng thông thường không yêu cầu sửa file:

1. Chạy Ops Console và mở **Sao lưu & phục hồi**.
2. Nhập kết nối DEV và TEST ngay ở đầu màn hình.
3. DEV cần schema và tài khoản để sao lưu.
4. TEST mới chỉ cần host, port và Service/PDB; schema chưa bắt buộc.
5. Khi restore, nhập schema đích và chọn dùng schema có sẵn hoặc tạo mới bằng SYS.

Dữ liệu được lưu như sau:

| Dữ liệu | Nơi lưu | Bảo vệ |
|---|---|---|
| Endpoint, schema, chính sách | `config/environments.json` | Không chứa password, bị Git ignore |
| Tên đăng nhập/mật khẩu | `state/oracle-credentials.protected` | Data Protection; DPAPI trên Windows |
| Data Protection key | `state/data-protection` | DPAPI theo tài khoản chạy app trên Windows |
| Bản sao DB | `<BackupRoot>/repository` | Nằm ngoài source/publish; service account có quyền ghi |

Không sao chép file tài khoản mã hóa sang máy khác và kỳ vọng dùng lại được.
Khi đổi máy hoặc đổi service account, nhập lại tài khoản trên giao diện.

### Biến môi trường dành cho server/CI

Biến môi trường vẫn được hỗ trợ và có độ ưu tiên cao hơn tài khoản lưu từ UI:

```powershell
$env:THUCLUC_OPS_DEV_DB_USER = "<dev-user>"
$env:THUCLUC_OPS_DEV_DB_PASSWORD = "<dev-password>"
$env:THUCLUC_OPS_TEST_DB_USER = "<test-user>"
$env:THUCLUC_OPS_TEST_DB_PASSWORD = "<test-password>"

```

Ý nghĩa đoạn PowerShell trên:

- `$env:TEN_BIEN = "giá trị"` tạo biến môi trường trong cửa sổ PowerShell hiện tại.
- Khi chạy `dotnet run`, tiến trình mini app kế thừa các biến này.
- `<dev-user>` và `<dev-password>` là chỗ giữ chỗ, phải thay bằng giá trị thật;
  không nhập nguyên dấu `<` và `>`.
- Đóng cửa sổ PowerShell thì các biến được đặt theo cách này mất đi.
- Không đặt các giá trị thật vào tài liệu, Git hoặc ảnh chụp màn hình.
- Nếu đã lưu tài khoản trên giao diện thì không cần đặt bốn biến này.
- Nếu cả UI và biến môi trường đều có tài khoản, execution layer sử
  dụng biến môi trường.
- Nếu cả hai đều chưa có, mini app vẫn chạy; kết quả kiểm tra báo
  **Tài khoản DB: chưa nhập** và các thao tác DB tiếp tục bị khóa.

Trong môi trường máy chủ, không nhập tài khoản thủ công mỗi lần. Thông tin này sẽ
được cấp cho Windows Service/service account hoặc kho bí mật trong quá trình cài đặt.

### Cấu hình kho backup trên Windows và Linux

Hai đường dẫn được khai báo đồng thời; miniapp tự chọn theo hệ điều hành đang chạy:

```json
"BackupRootLinux": "/var/lib/thucluc-db-manager/backups",
"BackupRootWindows": "D:\\ThucLucData\\DbBackups",
"DataPumpExportVersion": "COMPATIBLE"
```

Không cần comment hoặc đổi khóa khi chuyển máy. Khi khởi động, miniapp tạo `repository`, `inbox`,
`work` và `quarantine`. Việc tự tạo chỉ thành công khi tài khoản chạy service có quyền ghi vào thư
mục cha. Trên Linux production nên tạo và cấp quyền trước:

```bash
sudo install -d -o thucluc-dbmanager -g thucluc-dbmanager -m 0750 \
  /var/lib/thucluc-db-manager/backups
```

Phân vùng lưu trữ:

```text
<BackupRoot>/
├── repository/   # Bản sao đã hoàn chỉnh
├── inbox/        # File .dmp nhận từ công cụ ngoài
├── work/         # Vùng xử lý tạm
└── quarantine/   # File lỗi hoặc chưa đạt kiểm tra
```

Có thể override khi triển khai mà không sửa JSON bằng biến môi trường
`Ops__BackupRootLinux` hoặc `Ops__BackupRootWindows`. Khóa `BackupRoot` cũ chỉ được giữ để tương
thích và sẽ được ưu tiên nếu vẫn còn giá trị.

Mở URL localhost được in trong terminal, vào **Tổng quan** và chọn
**Kiểm tra tất cả**. Chưa thực hiện backup/restore nếu bất kỳ kiểm tra bắt buộc
nào còn màu đỏ.

### Ánh xạ quy trình sang giao diện

| Công việc | Vị trí trên Ops Console | Trạng thái hiện tại |
|---|---|---|
| Thêm/sửa DEV/TEST | Sao lưu & phục hồi → Kết nối cơ sở dữ liệu | Đã hoạt động |
| Lưu tài khoản DB mã hóa | Sao lưu & phục hồi → Kết nối cơ sở dữ liệu | Đã hoạt động |
| Kiểm tra DEV/TEST | Tổng quan → Kiểm tra kết nối | Đã hoạt động |
| Chuẩn bị tạo schema đích | Phục hồi → Tạo schema mới | Đã hoạt động; schema chỉ được tạo khi restore chạy |
| Xem lỗi từng môi trường | Tổng quan → Kiểm tra | Đã hoạt động |
| Backup DEV/TEST | Sao lưu & phục hồi → Tạo bản sao lưu | Đã hoạt động; cần kiểm thử với Oracle Directory thật |
| Nhận `.dmp` từ máy trạm | Phục hồi DB TEST → Tải file `.dmp` | Đã hoạt động; inbox → kiểm tra SHA-256 → repository |
| Restore vào schema trống | Sao lưu & phục hồi → Phục hồi vào DB đích | Đã hoạt động; cần kiểm thử Oracle thật |
| Thay thế schema có dữ liệu | Phục hồi → Thay thế toàn bộ schema | Đã hoạt động; bắt buộc backup an toàn và xác nhận tên schema |
| Clone DEV → TEST | Sao lưu & phục hồi → Clone | Đang khóa |
| Flyway info/validate/migrate | Migration | Đang khóa |
| Theo dõi job | Nhật ký vận hành | Có khung, chưa lưu bền vững |

Các câu lệnh `db-tool.ps1` ở phần dưới là CLI dự kiến dành cho xử lý khẩn cấp
và CI/CD. Khi mini app hoàn thiện, thao tác thông thường sẽ thực hiện trên giao
diện thay vì yêu cầu người vận hành nhớ câu lệnh.

## 4. Quy trình dựng TEST lần đầu

### Bước 1: Chuẩn bị hạ tầng TEST

Người quản trị cần chuẩn bị:

- Oracle có major version tương thích với DEV.
- Oracle service/PDB cho TEST.
- PDB TEST và tablespace phù hợp; schema TEST có thể được tạo trong chính luồng restore bằng SYS.
- Oracle `DIRECTORY` dùng cho Data Pump.
- Tài khoản thao tác có đủ quyền export/import schema.
- MinIO và bucket dành riêng cho TEST.
- Gotenberg dành cho chức năng xuất PDF.
- Máy triển khai có thể kết nối tới cả DEV và TEST.

### Bước 2: Kiểm tra trước khi sao chép

Công cụ cần kiểm tra:

- Kết nối DEV và TEST.
- Oracle version và character set.
- Schema nguồn và schema đích.
- Dung lượng trống tại nơi lưu dump.
- Quyền truy cập Oracle `DIRECTORY`.
- Flyway version hiện tại của DEV.
- TEST chưa có dữ liệu cần giữ, hoặc đã được backup.

Trên Ops Console, mở **Tổng quan** và bấm kiểm tra từng kết nối. Riêng DB TEST mới chưa có
schema chỉ cần kiểm tra được host/port và đúng Service/PDB; schema được kiểm tra trong bước phục hồi.

### Bước 3: Export schema DEV

Trên **Sao lưu & phục hồi**, chọn DB DEV rồi bấm **Tạo bản sao lưu**.

Công cụ sử dụng Oracle Data Pump để tạo:

```text
<BackupRoot>/repository/dev/<backup-id>/
├── database.dmp
├── export.log
└── manifest.json
```

`manifest.json` nên chứa:

- Môi trường và schema nguồn.
- Thời gian export.
- Oracle version.
- Flyway version.
- Git commit hoặc build number.
- Tên file dump, kích thước và checksum.
- Người thực hiện.

Lưu ý: Oracle Data Pump ghi file vào Oracle `DIRECTORY` trên máy chủ database. Nếu Oracle chạy trong
Docker cục bộ, Ops Console tự nhận diện container qua port và dùng `docker cp`; với DB từ xa mới dùng
Oracle `BFILE` dự phòng. File được lưu vào `<BackupRoot>/repository/<môi-trường>/<backup-id>/`, rồi tính
SHA-256. Tài khoản cần quyền export schema và quyền `READ`, `WRITE` trên `DIRECTORY`. Bản trên
máy chủ Oracle chưa bị tự động xóa để tránh xóa nhầm dữ liệu vận hành. Khi làm restore trên máy
Oracle khác, công cụ sẽ cần chuyển bản dump đã xác minh sang `DIRECTORY` của DB đích.

### Bước 4: Backup TEST nếu cần

Nếu TEST đã có dữ liệu:

```powershell
.\db-tool.ps1 backup -Env test -Tag before-initial-restore
```

Nếu TEST hoàn toàn mới thì có thể bỏ qua bước này.

### Bước 5: Restore vào TEST

1. Lưu kết nối TEST bằng host, port và Service/PDB.
2. Chọn bản sao đã hoàn tất. Với dump từ công cụ khác, bấm **Tải file .dmp**, chọn file trên
   máy trạm và khai báo đúng schema nguồn trong dump. Miniapp ghi vào `inbox`, tính SHA-256 rồi
   chuyển sang `repository/uploaded/<backup-id>` để có thể chọn phục hồi.
3. Chọn DB đích và nhập schema đích.
4. Chọn **Dùng schema trống đã có**, **Tạo schema mới** hoặc **Thay thế toàn bộ schema đang có**.
5. Với schema trống: nhập mật khẩu schema. Với tạo mới hoặc thay thế: nhập SYS, mật khẩu schema sau
   phục hồi, kiểm tra PDB và chọn tablespace.
6. Với chế độ thay thế, nhập chính xác tên schema để xác nhận. Miniapp sẽ tạo bản sao an toàn của
   schema đích hiện tại và chỉ tiếp tục khi dump đã được tải về kho, kiểm tra SHA-256 thành công.
7. Đọc tuyến nguồn → đích, nội dung xác nhận rồi bấm nút phục hồi.

Miniapp xác minh dung lượng và SHA-256, chuyển dump cục bộ vào Oracle Directory của DB đích, đối
chiếu lại dung lượng file trên máy chủ Oracle rồi chạy `DBMS_DATAPUMP` import. Nếu tên schema khác,
công cụ tự `REMAP_SCHEMA`; thuộc tính vật lý của
segment nguồn được bỏ để đối tượng dùng tablespace mặc định của schema đích.

Manifest lưu cả phiên bản Oracle nguồn và phiên bản thực tế của dump. Giá trị mặc định
`Ops:DataPumpExportVersion = COMPATIBLE` chỉ dùng cho bản sao độc lập khi chưa kiểm tra DB đích.
Sau khi người dùng bấm **Kiểm tra DB đích**, miniapp lấy cả phiên bản phần mềm và tham số database
`COMPATIBLE`, rồi export với phiên bản cao nhất không vượt quá cả nguồn lẫn `COMPATIBLE` của đích.
Trước mọi thay đổi schema, miniapp đọc phiên bản dump,
kiểm tra DB đích và chặn nếu dump mới hơn. File dump đã tạo không thể đổi mức tương thích; phải
export lại từ nguồn.

Nếu phát hiện dump mới hơn DB đích, giao diện khóa nút phục hồi và hiện nút
**Tạo bản sao tương thích Oracle x.y**. Miniapp export lại từ kết nối nguồn với `VERSION` bằng phiên
bản tương thích DB đích (ví dụ Oracle 19c có `COMPATIBLE=12.2` thì dùng `VERSION=12.2`), sau đó tự
chọn manifest mới. Bản
dump cũ vẫn được giữ để có thể dùng với DB cùng phiên bản nguồn.

Nếu schema mới đã được tạo nhưng import lỗi, lần **Kiểm tra DB đích** tiếp theo sẽ nhận ra schema
đang trống và tự chuyển sang **Dùng schema trống đã có**. Không tạo lại hoặc xóa thủ công; nhập đúng
mật khẩu schema vừa tạo và phục hồi lại bằng dump tương thích.

Kiểm tra DB hiển thị thêm timezone file version (TSTZ). Nếu TSTZ nguồn lớn hơn TSTZ đích, miniapp
dừng trước khi tạo hoặc thay đổi schema đích. `VERSION` của Data Pump chỉ điều chỉnh định dạng dump,
không hạ timezone file trong dữ liệu. DBA phải nâng timezone file DB đích lên cùng hoặc cao hơn nguồn
bằng quy trình Oracle `DBMS_DST`/`utltz_*`; đây là thay đổi cấp database có thời gian dừng dịch vụ,
không được tự động hóa trong Ops Console.

Phương án đã chọn cho môi trường hiện tại là cài Oracle XE 21c song song với Oracle 19, giữ Oracle 19
ở cổng `1521` và dùng `XEPDB1` của XE 21c ở cổng `1522`. Thực hiện theo runbook
[Cài Oracle Database XE 21c trên Linux song song với Oracle 19](oracle-xe21-ol8-side-by-side-installation.md).

DBA kiểm tra trên DB đích trước khi lập lịch nâng cấp:

```sql
SELECT FILENAME, VERSION FROM V$TIMEZONE_FILE;

SELECT PROPERTY_NAME, PROPERTY_VALUE
FROM DATABASE_PROPERTIES
WHERE PROPERTY_NAME LIKE 'DST_%'
ORDER BY PROPERTY_NAME;
```

Kiểm tra tại `$ORACLE_HOME/oracore/zoneinfo` (Linux) hoặc `%ORACLE_HOME%\oracore\zoneinfo` (Windows)
có `timezlrg_35.dat` hoặc bản mới hơn. Nếu chưa có, cập nhật Oracle 19c RU/DST patch trước. Sau khi
backup toàn bộ DB, DBA thực hiện prepare/upgrade window theo tài liệu Oracle `DBMS_DST`, kiểm tra
`DBA_TSTZ_TABLES` và chỉ mở lại phục hồi khi `V$TIMEZONE_FILE.VERSION >= 35`.

Trạng thái `COMPLETED` của Data Pump là kết quả chính của phục hồi. Việc tải log về máy Ops Console là
hậu xử lý; lỗi đọc/đóng `BFILE` chỉ tạo cảnh báo, không được đổi một import đã hoàn tất thành thất bại.

Không có import chồng vào schema có đối tượng. Ở chế độ thay thế, miniapp khóa schema, ngắt các session
của schema đích rồi chạy `DROP USER ... CASCADE` sau khi backup an toàn hoàn tất; sau đó tạo lại schema
và import. Dù công cụ có xử lý session, quy trình vận hành vẫn phải dừng backend trước để tránh hủy
giao dịch đang chạy.
Mật khẩu schema không bị miniapp ép tối thiểu 12 ký tự; profile/password verify function của Oracle
đích mới là nơi quyết định mật khẩu có được chấp nhận hay không.

### Bước 6: Kiểm tra và nâng phiên bản DB

```powershell
.\db-tool.ps1 validate -Env test
.\db-tool.ps1 info -Env test
.\db-tool.ps1 migrate -Env test
```

Ví dụ dump DEV đang ở V89 nhưng source code chuẩn bị deploy có V90 và V91:

```text
Restore V89 → Flyway V90 → Flyway V91 → DB sẵn sàng cho ứng dụng mới
```

Nếu dump và source code cùng ở V89, lệnh migrate không thay đổi dữ liệu và chỉ xác nhận không có migration đang chờ.

### Bước 7: Đồng bộ file MinIO

Nếu dữ liệu DB có bản ghi file đính kèm hoặc PDF, cần mirror bucket DEV sang bucket TEST.

Không đồng bộ MinIO sẽ dẫn tới trường hợp bản ghi file tồn tại trong DB nhưng nội dung object không tồn tại khi tải xuống.

Việc đồng bộ MinIO phải là tùy chọn độc lập vì có thể tốn dung lượng và chứa dữ liệu nhạy cảm.

### Bước 8: Deploy ứng dụng TEST

1. Cấu hình backend trỏ tới Oracle TEST, MinIO TEST và Gotenberg TEST.
2. Chạy health check của backend.
3. Deploy backend.
4. Build/deploy frontend trỏ tới API TEST.
5. Thực hiện smoke test.

Smoke test tối thiểu:

- Đăng nhập.
- Tra cứu đơn vị và danh mục.
- Thêm/sửa một bản ghi nghiệp vụ.
- Nộp báo cáo.
- Tra cứu snapshot.
- Xuất Excel.
- Xem/xuất PDF.
- Tải lên và tải xuống file đính kèm.

## 5. Quy trình deploy một phiên bản code mới lên TEST

### Việc thực hiện trên DEV

1. Khi sửa cấu trúc DB, tạo migration mới, ví dụ `V90__add_xxx.sql`.
2. Chạy migration trên DEV.
3. Chạy ứng dụng và kiểm thử chức năng liên quan.
4. Commit migration cùng source code.
5. Không đưa mật khẩu hoặc file dump vào Git.

Lệnh dự kiến:

```powershell
.\db-tool.ps1 validate -Env dev
.\db-tool.ps1 migrate -Env dev
```

### Việc thực hiện trên TEST

```text
Preflight
    ↓
Backup TEST
    ↓
Validate Flyway
    ↓
Migrate TEST
    ↓
Deploy backend/frontend cùng phiên bản
    ↓
Health check và smoke test
```

Lệnh dự kiến:

```powershell
.\db-tool.ps1 preflight -Env test
.\db-tool.ps1 backup -Env test -Tag before-v90
.\db-tool.ps1 validate -Env test
.\db-tool.ps1 migrate -Env test
```

Không restore DEV vào TEST trong quy trình deploy thông thường. Dữ liệu tester đang nhập trên TEST phải được giữ nguyên.

## 6. Quy trình làm mới dữ liệu TEST từ DEV

Chỉ thực hiện khi tester và người quản lý môi trường đã đồng ý xóa dữ liệu TEST hiện tại.

Lệnh gộp dự kiến:

```powershell
.\db-tool.ps1 clone -From dev -To test
```

Luồng xử lý bên trong:

```text
Kiểm tra DEV và TEST
    ↓
Hiển thị dữ liệu TEST sẽ bị thay thế
    ↓
Backup TEST
    ↓
Export DEV
    ↓
Chuyển dump tới TEST
    ↓
Restore TEST
    ↓
Validate và migrate tới phiên bản source mục tiêu
    ↓
Mask/đổi thông tin nhạy cảm và tài khoản TEST
    ↓
Tùy chọn mirror MinIO
    ↓
Smoke test
```

Sau khi clone cần xem xét:

- Đổi mật khẩu tài khoản quản trị TEST.
- Vô hiệu hóa tài khoản không dùng cho kiểm thử.
- Mask dữ liệu nhạy cảm nếu có.
- Không gửi email/SMS/thông báo thật từ TEST.
- Thay URL hoặc endpoint tích hợp ngoài bằng endpoint giả lập.

## 7. Quy trình phục hồi TEST sau sự cố

1. Dừng backend TEST hoặc bật maintenance mode.
2. Xác định bản backup cần phục hồi.
3. Kiểm tra checksum và manifest.
4. Backup trạng thái lỗi hiện tại nếu còn khả năng phân tích.
5. Restore bản backup đã chọn.
6. Chạy Flyway validate/info.
7. Chỉ chạy migrate nếu phiên bản ứng dụng yêu cầu DB mới hơn bản backup.
8. Khởi động backend.
9. Thực hiện health check và smoke test.

Nếu schema đích đang có dữ liệu, chọn **Thay thế toàn bộ schema đang có**. Ops Console bắt buộc tạo
bản sao an toàn, xác minh SHA-256, yêu cầu xác nhận đúng tên schema rồi mới xóa/tạo lại và import.
Nếu backup an toàn thất bại thì schema hiện tại không bị xóa. Flyway trên giao diện vẫn chưa được mở
và cần chạy bằng quy trình triển khai hiện có.

## 8. PowerShell và mini app có giao diện

### PowerShell `.ps1`

`.ps1` là script PowerShell, phù hợp nhất khi chạy trên Windows. PowerShell 7 có thể chạy trên Linux, nhưng các chi tiết như đường dẫn, Oracle client và cơ chế chuyển file vẫn cần thiết kế theo hệ điều hành thực tế.

Ưu điểm:

- Làm nhanh.
- Dễ kiểm tra từng câu lệnh.
- Phù hợp để chuẩn hóa nghiệp vụ trước khi làm giao diện.
- Có thể tái sử dụng trong CI/CD.

Hạn chế:

- Người dùng phải nhớ câu lệnh và tham số.
- Thao tác restore dễ nhập nhầm môi trường nếu bảo vệ không đủ tốt.
- Theo dõi tiến trình và lịch sử chưa trực quan.

### Mini app được đề xuất

Có thể xây dựng một ứng dụng quản trị DB nhỏ bằng .NET 8, chạy độc lập với ứng dụng nghiệp vụ và mở giao diện qua trình duyệt.

Phương án khuyến nghị:

```text
ops/db-manager/
├── src/ThucLuc.DbManager/
│   ├── ASP.NET Core / Razor Pages hoặc Blazor Server
│   └── Các service điều phối Data Pump, Flyway và MinIO
├── scripts/
│   └── db-tool.ps1
├── config/
│   └── environments.example.json
└── README.md
```

Mini app chạy trên máy triển khai hoặc máy quản trị nội bộ, chỉ bind localhost
hoặc mạng quản trị và không public ra Internet.

Không nên đưa chức năng backup/restore vào frontend hoặc API quản trị của hệ
thống Thực Lực vì ứng dụng nghiệp vụ không nên nắm tài khoản có quyền Data Pump
hoặc quyền reset schema.

### Màn hình dự kiến

#### Tổng quan môi trường

- DEV/TEST đang online hay offline.
- Oracle version.
- Schema hiện tại.
- Flyway version hiện tại và version mới nhất trong source.
- Số migration đang chờ.
- Lần backup gần nhất.
- Dung lượng backup.

#### Backup

- Chọn môi trường.
- Nhập nhãn/tag.
- Tùy chọn backup schema và metadata cần thiết.
- Hiển thị tiến trình và log.
- Cho phép tải/xem manifest, không cho tải dump nếu chính sách bảo mật không cho phép.

#### Restore

- Chỉ cho chọn TEST trong phiên bản đầu.
- Chọn bản backup từ danh sách.
- Hiển thị rõ nguồn, thời gian, Flyway version và kích thước.
- Tự động tạo backup TEST trước restore.
- Yêu cầu nhập chuỗi xác nhận.
- Hiển thị tiến trình từng bước.

#### Migration

- Flyway info/validate.
- Danh sách migration đã chạy, đang chờ hoặc lỗi.
- Nút chạy migrate.
- Tự động backup trước migration nếu có pending migration.

#### Clone DEV sang TEST

- Hiển thị cảnh báo toàn bộ dữ liệu TEST sẽ bị thay thế.
- Cho phép chọn có/không mirror MinIO.
- Cho phép cấu hình mask dữ liệu.
- Yêu cầu xác nhận hai lớp.

#### Lịch sử vận hành

- Ai thực hiện.
- Thời gian bắt đầu/kết thúc.
- Môi trường nguồn và đích.
- Trạng thái thành công/thất bại.
- Log `expdp`, `impdp` và Flyway.
- Git commit/build number liên quan.

### Kiến trúc công cụ đề xuất

```text
Giao diện web nội bộ
        ↓
DbManager Application Service
        ├── Preflight service
        ├── Flyway service
        ├── Oracle Data Pump service
        ├── Backup catalog/manifest service
        ├── File transfer service
        ├── MinIO mirror service
        └── Audit log service
```

Backup dùng `DBMS_DATAPUMP` qua ODP.NET để mật khẩu không xuất hiện trên command line. Restore có
thể dùng API tương ứng hoặc `impdp`; Flyway phải được gọi bằng danh sách tham số an toàn, không ghép
trực tiếp dữ liệu người dùng thành chuỗi shell command.

## 9. Các lớp bảo vệ bắt buộc của mini app

- Chỉ truy cập từ localhost hoặc mạng quản trị.
- Có đăng nhập riêng hoặc Windows authentication.
- Không hiển thị mật khẩu trên UI/log.
- Secret lấy từ environment variable, secret store hoặc Windows Credential Manager.
- Danh sách môi trường được cấu hình sẵn; không cho nhập tùy ý connection string trên UI.
- PROD bị vô hiệu hóa restore theo code/config ở giai đoạn đầu.
- Mỗi lần restore tự động backup môi trường đích trước.
- Kiểm tra checksum file dump trước khi import.
- Chỉ cho một job thay đổi DB chạy tại một thời điểm.
- Có timeout, hủy job an toàn và lưu log đầy đủ.
- Không cho xóa backup trực tiếp nếu chưa có chính sách retention.

## 10. Các vấn đề hiện tại cần xử lý trước khi deploy

1. `backend/db/flyway/migrate.ps1` đang có mật khẩu mặc định trong tham số. Cần chuyển sang secret ngoài Git.
2. `R__seed_baseline_data.sql` đang chứa dữ liệu demo non-production. Cần tách seed hệ thống bắt buộc khỏi seed demo để tránh cập nhật lại dữ liệu TEST ngoài ý muốn.
3. `appsettings.Example.json` đang có connection user và `Database.Schema` không đồng nhất. Cần chuẩn hóa theo từng môi trường.
4. Chuyển trách nhiệm điều phối Flyway từ script riêng trong backend sang công cụ chung ở `ops/db-manager`; thư mục Flyway SQL vẫn thuộc backend.
5. `infra/docker-compose.yml` hiện mới có MinIO và Gotenberg, chưa mô tả backend, frontend, reverse proxy và cấu hình môi trường TEST.
6. Frontend cần có cấu hình build/runtime riêng để trỏ đúng API TEST, không sửa tay URL sau mỗi lần build.
7. Cần bổ sung quy trình backup/mirror MinIO cùng với DB khi muốn sao chép đầy đủ môi trường.
8. Cần bổ sung đường dẫn backup vào `.gitignore` để không commit file `.dmp`, `.log` hoặc manifest chứa thông tin môi trường.

## 11. Lộ trình triển khai công cụ

### Giai đoạn 1: Chuẩn hóa bằng command line

- Tạo khu vực chung `ops/db-manager`, không đặt trong backend.
- Xây dựng các service/lệnh `preflight`, `info`, `validate`, `migrate`, `backup`, `restore`.
- Thử nghiệm trên DEV và một schema TEST tạm.
- Chuẩn hóa manifest, log, xác nhận và xử lý lỗi.

### Giai đoạn 2: Mini app giao diện

- Dùng lại toàn bộ service của giai đoạn 1.
- Bổ sung dashboard, job progress và lịch sử vận hành.
- Bổ sung clone DEV → TEST và MinIO mirror.

### Giai đoạn 3: Tự động hóa deploy

- Hoàn thiện cấu hình `infra` cho backend, frontend, MinIO, Gotenberg và reverse proxy.
- Đóng gói quy trình backup → migrate → deploy backend/frontend → smoke test.
- Tích hợp CI/CD sau khi quy trình thủ công đã ổn định.
- Chỉ mở rộng sang STAGING/PROD khi đã có phân quyền, retention và phương án phục hồi được kiểm thử.

## 12. Các thông tin cần chốt trước khi viết công cụ

- Máy chủ TEST chạy Windows hay Linux.
- Oracle DEV và TEST nằm cùng máy, cùng PDB hay hai máy khác nhau.
- Phiên bản Oracle DEV/TEST.
- Có cài Oracle Client/Data Pump trên máy triển khai hay không.
- Quyền tạo Oracle `DIRECTORY` và quyền đọc/ghi thư mục dump.
- Cách chuyển dump giữa hai máy: thư mục dùng chung, SFTP/SCP hay thao tác trực tiếp trên DB server.
- Dung lượng DB hiện tại và thời gian backup tối đa cho phép.
- TEST có được phép chứa dữ liệu DEV nguyên bản hay bắt buộc mask dữ liệu.
- Có cần sao chép MinIO ngay trong lần dựng TEST đầu tiên hay không.
- Chính sách giữ backup: số bản, số ngày và vị trí lưu.

Sau khi chốt các thông tin trên mới quyết định chính xác mini app sẽ chạy ở máy nào và cách gọi Data Pump an toàn nhất.
